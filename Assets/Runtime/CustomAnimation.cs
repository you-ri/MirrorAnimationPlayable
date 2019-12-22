using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Unity.Collections;
using System.Collections;


namespace Lilium
{

    /// <summary>
    /// 拡張アニメーション制御
    /// </summary>
    public class CustomAnimation : MonoBehaviour
    {
        /// <summary>
        /// ステート名と関連するクリップ
        /// </summary>
        [System.Serializable]
        public struct StateInfo
        {
            public string name;

            public AnimationClip clip;

            public bool isValid {
                get {
                    return clip != null;
                }
            }
        }


        /// <summary>
        /// ミラー処理する追加ボーン
        /// </summary>
        [System.Serializable]
        private struct MirroringTransform
        {
            public Transform source;
            public Transform driven;
        }

        /// <summary>
        /// ミラー処理するメッシュ
        /// </summary>
        [System.Serializable]
        private struct MirroringConstraint
        {
            public Transform driven;
            public Transform mirrorParent;
        }

        public AnimationClip current;

        [Tooltip ("ステートリスト")]
        public List<StateInfo> clips;

        [Tooltip ("トランスフォームのミラー設定")]
        [SerializeField]
        private MirroringTransform[] mirroringTransforms;

        [Tooltip ("ミラーペアレント拘束")]
        [SerializeField]
        private MirroringConstraint[] mirroringConstraints;

        public bool debug = false;

        /// <summary>
        /// ミラー処理中か？
        /// </summary>
        public bool mirror
        {
            get { return _mirror; }
            set {
                if (_mirror != value) {
                    _mirror = value;
                }
            }
        }

        [SerializeField]
        private bool _mirror = false;


        /// <summary>
        /// アニメーター
        /// </summary>
        public Animator animator
        {
            get => _animator = _animator ? _animator : GetComponent<Animator> ();
        }
        private Animator _animator;

        public string currentStateName { get; private set; }

        PlayableGraph _graph;

        AnimationMixerPlayable _mixer;
        AnimationClipPlayable _preClipPlayable;
        AnimationClipPlayable _currentClipPlayable;
        AnimationScriptPlayable _mirroringPlayable;
        MirroringPlayableJob _mirroringPlayableJob;
        Coroutine _playAnimationCoroutine;

        public StateInfo FindStateInfo (string stateName)
        {
            return clips.Find ((c) => c.name == stateName);
        }

        public float GetSpeed ()
        {
            return (float)_currentClipPlayable.GetSpeed ();
        }

        public void SetSpeed (float value)
        {
            _currentClipPlayable.SetSpeed (value);
        }

        void Awake ()
        {
        }

        void OnEnable ()
        {
            // output <- _mirroringPlayable <- _mixer
            _graph = PlayableGraph.Create ($"{gameObject.name}.Animatrix");

            var output = AnimationPlayableOutput.Create (_graph, name, animator);

            _mixer = AnimationMixerPlayable.Create (_graph, 2, true);

            _mirroringPlayableJob = MirroringPlayableJob.Create (animator.BindStreamTransform (transform), mirroringTransforms.Length, mirroringConstraints.Length) ;
            _mirroringPlayable = AnimationScriptPlayable.Create (_graph, _mirroringPlayableJob);
            _mirroringPlayable.AddInput (_mixer, 0, 1.0f);

            output.SetSourcePlayable (_mirroringPlayable);

            // ミラーリング設定
            var mirroringPostures = new MirroringPlayableJob.MirroringPosture[mirroringTransforms.Length];
            for (int i = 0; i < mirroringPostures.Length; i++) {
                mirroringPostures[i].source = animator.BindStreamTransform (mirroringTransforms[i].source);
                mirroringPostures[i].driven = animator.BindStreamTransform (mirroringTransforms[i].driven);
            }
            _mirroringPlayableJob.mirroringTransforms.CopyFrom (mirroringPostures);

            var mirroringParentConstraintsData = new MirroringPlayableJob.MirroringConstrant[mirroringConstraints.Length];
            for (int i = 0; i < mirroringParentConstraintsData.Length; i++) {
                mirroringParentConstraintsData[i].driven = animator.BindStreamTransform (mirroringConstraints[i].driven);
                mirroringParentConstraintsData[i].source = animator.BindStreamTransform (mirroringConstraints[i].mirrorParent);
            }
            _mirroringPlayableJob.mirroringConstrants.CopyFrom (mirroringParentConstraintsData);

            _mirroringPlayable.SetJobData (_mirroringPlayableJob);

            _graph.Play ();
        }

        void OnDisable ()
        {
            _mirroringPlayableJob.Dispose ();
            _graph.Destroy ();
        }

        void Start ()
        {
            if (current != null) {
                _PlayInternal (current);
            }
        }

        void OnDestroy ()
        {
        }


        private void Update ()
        {
            Apply ();
        }


        /// <summary>
        /// 反映
        /// </summary>
        public void Apply ()
        {
            if (!enabled) return;

            var _animatrixPlayableJob = _mirroringPlayable.GetJobData<MirroringPlayableJob> ();
            _animatrixPlayableJob.isMirror = mirror;
            _animatrixPlayableJob.debug = debug;
            _mirroringPlayable.SetJobData (_animatrixPlayableJob);
        }


        /// <summary>
        /// アニメーションクリップをステートに設定して再生する。
        /// </summary>
        /// <param name="stateName">アニメーション名</param>
        /// <param name="clip"></param>
        /// <param name="speed">アニメーション速度</param>
        /// <param name="fadeLength">クロスフェード時間</param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        public void Play (
            AnimationClip clip,
            float speed = 1.0f,
            float fadeLength = 0.0f,
            float startTime = 0.0f,
            float endTime = 0.0f,
            float rootMotionWeight = 1.0f)
        {
            Debug.Assert (speed != 0);
            Debug.Assert (clip != null);

            //print ($"PlayClipAtState PlayInFixedTime {clip.name} {startTime}");

            if (fadeLength == 0) {
                _PlayInternal (clip, startTime);
            }
            else {
                _CrossFadeInternal (clip, fadeLength, startTime);
            }

            SetSpeed (speed);
 
            currentStateName = clip.name;
        }

        /// <summary>
        /// アニメーション再生
        /// Animator ステート名を指定して再生
        /// </summary>
        /// <param name="stateName">アニメーションステート名</param>
        /// <param name="speed">アニメーション速度</param>
        /// <param name="fadeLength">クロスフェード時間</param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        public void Play (string stateName, float speed = 1.0f, float fadeLength = 0.0f, float startTime = 0.0f, float endTime = 0.0f)
        {
            Debug.Assert (_graph.IsPlaying());
            Debug.Assert (speed != 0);

            if (fadeLength == 0) {
                this._PlayInternal (stateName, startTime);
            }
            else {
                this._CrossFadeInternal (stateName, fadeLength, startTime);
            }
            SetSpeed (speed);

            currentStateName = stateName;
        }

        /// <summary>
        /// アニメーションの一時停止
        /// </summary>
        public void Pause ()
        {
            SetSpeed (0);
        }


        /// <summary>
        /// 指定したアニメーション再生中か？
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsPlayingAnimation (string name)
        {
            if (currentStateName == name)
                return true;

            return false;
        }


        void _CrossFadeInternal (string stateName, float fadeLength, float startTime = 0)
        {
            StateInfo stateInfo = FindStateInfo (stateName);
            if (!stateInfo.isValid) {
                Debug.LogWarning ($"[Animatrix] state not found at '{this.name}' state:{stateName}");
                return;
            }
            _CrossFadeInternal (stateInfo.clip, fadeLength, startTime);

        }

        void _CrossFadeInternal (AnimationClip clip, float fadeLength, float startTime = 0)
        {
            Debug.Assert (clip != null);

            if (debug) Debug.Log ($"[Animatrix] CrossFadeIntarenal clip:{clip.name}");

            if (_playAnimationCoroutine != null)
                StopCoroutine (_playAnimationCoroutine);
            _playAnimationCoroutine = StartCoroutine (_CrossfadeAnimationCoroutine (clip, fadeLength, startTime));

            current = clip;
        }

        void _PlayInternal (string stateName, float startTime = 0)
        {
            StateInfo stateInfo = FindStateInfo (stateName);
            if (!stateInfo.isValid) {
                Debug.LogWarning ($"[Animatrix] state not found at '{this.name}'. state:{stateName}");
                return;
            }

            _PlayInternal (stateInfo.clip, startTime);
        }

        void _PlayInternal (AnimationClip clip, float startTime = 0)
        {
            Debug.Assert (clip != null);

            if (debug) Debug.Log ($"[Animatrix] PlayInternal clip:{clip.name}");

            if (_playAnimationCoroutine != null)
                StopCoroutine (_playAnimationCoroutine);

            if (_currentClipPlayable.IsValid ()) {
                _currentClipPlayable.Destroy ();
            }
            _DisconnectAnimationClipPlayables ();
            _currentClipPlayable = AnimationClipPlayable.Create (_graph, clip);
            _currentClipPlayable.SetTime (startTime);
            _currentClipPlayable.SetApplyFootIK (false);

            _mixer.ConnectInput (0, _currentClipPlayable, 0);

            _mixer.SetInputWeight (0, 1);
            _mixer.SetInputWeight (1, 0);

            current = clip;
        }



        IEnumerator _CrossfadeAnimationCoroutine (AnimationClip newAnimation, float transitionTime, float startTime)
        {
            _DisconnectAnimationClipPlayables ();

            _preClipPlayable = _currentClipPlayable;
            _currentClipPlayable = AnimationClipPlayable.Create (_graph, newAnimation);
            _currentClipPlayable.SetTime (startTime);
            _currentClipPlayable.SetApplyFootIK (false);

            _mixer.ConnectInput (0, _currentClipPlayable, 0);
            _mixer.ConnectInput (1, _preClipPlayable, 0);

            // 指定時間でアニメーションをブレンド
            float waitTime = Time.timeSinceLevelLoad + transitionTime;
            yield return new WaitWhile (() => {
                var diff = waitTime - Time.timeSinceLevelLoad;
                if (diff <= 0) {
                    _mixer.SetInputWeight (0, 1);
                    _mixer.SetInputWeight (1, 0);
                    return false;
                }
                else {
                    var rate = Mathf.Clamp01 (diff / transitionTime);
                    _mixer.SetInputWeight (0, 1 - rate);
                    _mixer.SetInputWeight (1, rate);
                    return true;
                }
            });

            _playAnimationCoroutine = null;

        }

        void _DisconnectAnimationClipPlayables ()
        {
            _graph.Disconnect (_mixer, 0);
            _graph.Disconnect (_mixer, 1);
            if (_preClipPlayable.IsValid ())
                _preClipPlayable.Destroy ();
        }
    }
}