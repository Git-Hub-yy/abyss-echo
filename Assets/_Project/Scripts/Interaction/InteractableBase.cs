using UnityEngine;

namespace AbyssEcho.Interaction
{
    /// <summary>
    /// 可交互物体基类：处理通用的"提示文本 / 可交互开关 / 视线高亮"逻辑。
    ///
    /// 派生类只需重写 <see cref="OnInteract"/> 与 <see cref="DefaultPrompt"/>，
    /// 其余交互探测、高亮、状态管理都在这里完成，避免每个物体重复实现。
    ///
    /// 高亮实现：轻微放大 + 材质自发光，不依赖后处理描边（性能友好）。
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("提示")]
        [Tooltip("准星提示文本；留空则使用子类默认文本")]
        public string customPrompt = "";

        [Tooltip("当前是否可交互")]
        public bool interactable = true;

        [Header("高亮")]
        [Tooltip("被注视时是否高亮")]
        public bool highlightOnFocus = true;

        [Tooltip("高亮时放大的比例")]
        public float highlightScale = 1.06f;

        [Tooltip("高亮颜色（自发光）")]
        public Color highlightColor = new Color(0.45f, 0.75f, 1f);

        protected Vector3 _baseScale;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _focused;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public virtual bool CanInteract => interactable && isActiveAndEnabled;

        /// <summary>子类提供的默认提示文本。</summary>
        protected abstract string DefaultPrompt { get; }

        protected virtual void Awake()
        {
            _baseScale = transform.localScale;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        public virtual string GetPrompt()
        {
            if (!string.IsNullOrEmpty(customPrompt)) return customPrompt;
            return interactable ? DefaultPrompt : "无法使用";
        }

        public void OnFocusEnter()
        {
            _focused = true;
            if (!highlightOnFocus) return;

            transform.localScale = _baseScale * highlightScale;
            SetEmission(highlightColor * 0.55f);
        }

        public void OnFocusExit()
        {
            if (!_focused) return;
            _focused = false;

            transform.localScale = _baseScale;
            SetEmission(Color.black);
        }

        /// <summary>设置子渲染器的自发光（用 MPB，不产生材质实例）。</summary>
        protected void SetEmission(Color c)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        public abstract void OnInteract(GameObject interactor);
    }
}