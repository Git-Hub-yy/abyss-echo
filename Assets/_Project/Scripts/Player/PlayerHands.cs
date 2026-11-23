using UnityEngine;

namespace AbyssEcho.Player
{
    /// <summary>
    /// 第一人称手部代理（S2，程序化）。
    ///
    /// 背景：当前无网络，无法获得手部模型。此脚本用 Unity 图元拼出一个"可信的右手 + 手电筒"，
    /// 用于：
    ///  1) 提前打通"持物 / 伸手 / 走路摆动"的动画逻辑；
    ///  2) 提供手电筒的视觉落点，避免光束"凭空出现"。
    ///
    /// 将来替换真实模型时：只需替换 HandRoot 下的视觉子物体，**不需要改动任何脚本**。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHands : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("手部根节点（挂在相机下）")]
        [SerializeField] private Transform handRoot;
        [Tooltip("输入源（留空自动查找）")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("移动组件（用于判断是否在走路/奔跑）")]
        [SerializeField] private PlayerMotor motor;
        [Tooltip("相机（用于把手的相对位置固定到屏幕右下角）")]
        [SerializeField] private Camera playerCamera;

        [Header("基础位置（相机局部坐标）")]
        [SerializeField] private Vector3 restLocalPosition = new Vector3(0.22f, -0.22f, 0.35f);
        [SerializeField] private Vector3 restLocalEuler = new Vector3(2f, -8f, 0f);

        [Header("走路摆动")]
        [Tooltip("摆动幅度（米）")]
        [SerializeField] private float bobAmplitude = 0.012f;
        [Tooltip("摆动频率（与速度相关）")]
        [SerializeField] private float bobFrequency = 8.5f;
        [Tooltip("走路时的手臂摇晃角度")]
        [SerializeField] private float swayDegrees = 1.6f;

        [Header("伸手动画（交互时）")]
        [Tooltip("伸手时向前移动的距离")]
        [SerializeField] private float reachForward = 0.12f;
        [Tooltip("伸手过渡时间")]
        [SerializeField] private float reachDuration = 0.15f;

        [Header("生成简易手部（若 HandRoot 为空则自动生成）")]
        [SerializeField] private bool autoBuildPlaceholder = true;

        private float _bobPhase;
        private float _reachT;          // 0 = 收起, 1 = 伸出
        private float _reachTarget;
        private Vector3 _reachReturnAt;

        private void Awake()
        {
            if (input == null) input = GetComponentInParent<PlayerInputReader>();
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (playerCamera == null) playerCamera = GetComponentInParent<Camera>();

            if (handRoot == null && autoBuildPlaceholder)
                handRoot = BuildPlaceholderHand();

            if (handRoot != null)
            {
                handRoot.localPosition = restLocalPosition;
                handRoot.localRotation = Quaternion.Euler(restLocalEuler);
            }
        }

        private void OnEnable()
        {
            if (input != null) input.OnInteract += TriggerReach;
        }

        private void OnDisable()
        {
            if (input != null) input.OnInteract -= TriggerReach;
        }

        private void LateUpdate()
        {
            if (handRoot == null) return;

            float dt = Time.deltaTime;

            // 1) 走路摆动：频率随速度变化
            float speed = motor != null ? motor.CurrentSpeed : 0f;
            bool moving = speed > 0.2f;

            if (moving)
                _bobPhase += dt * bobFrequency * Mathf.Clamp(speed / 2.2f, 0.7f, 1.8f);
            else
                _bobPhase = Mathf.Lerp(_bobPhase, Mathf.Round(_bobPhase / Mathf.PI) * Mathf.PI, dt * 4f);

            float bobY = Mathf.Sin(_bobPhase * 2f) * bobAmplitude * (moving ? 1f : 0.15f);
            float bobX = Mathf.Cos(_bobPhase) * bobAmplitude * 0.6f * (moving ? 1f : 0.15f);

            // 2) 伸手
            float reachSpeed = 1f / Mathf.Max(0.01f, reachDuration);
            _reachT = Mathf.MoveTowards(_reachT, _reachTarget, dt * reachSpeed);

            // 3) 合成位置与旋转
            Vector3 pos = restLocalPosition + new Vector3(bobX, bobY, _reachT * reachForward);
            Quaternion rot = Quaternion.Euler(restLocalEuler) *
                             Quaternion.Euler(-_reachT * 10f, _reachT * 6f, 0f) *
                             Quaternion.Euler(Mathf.Cos(_bobPhase) * swayDegrees, 0f, 0f);

            handRoot.localPosition = Vector3.Lerp(handRoot.localPosition, pos, 1f - Mathf.Exp(-14f * dt));
            handRoot.localRotation = Quaternion.Slerp(handRoot.localRotation, rot, 1f - Mathf.Exp(-14f * dt));
        }

        /// <summary>触发一次"伸手"动作（交互时调用；InteractionSystem 也会调用）。</summary>
        public void TriggerReach()
        {
            StopAllCoroutines();
            StartCoroutine(ReachRoutine());
        }

        private System.Collections.IEnumerator ReachRoutine()
        {
            _reachTarget = 1f;
            yield return new WaitForSeconds(Mathf.Max(0.05f, reachDuration * 1.6f));
            _reachTarget = 0f;
        }

        // ────────────────────────── 程序化手部 ──────────────────────────

        /// <summary>
        /// 生成一个"右手 + 手电筒"的简易代理。
        /// 结构：HandRoot → Forearm(胶囊) + Palm(方块) + Flashlight(圆柱+透镜) + SpotLight
        /// </summary>
        private Transform BuildPlaceholderHand()
        {
            var root = new GameObject("HandRoot_Placeholder").transform;
            root.SetParent(transform, false);

            var metalMat = CreateMaterial("M_HandProxy_Metal", new Color(0.30f, 0.32f, 0.34f), 0.85f, 0.55f);
            var gloveMat = CreateMaterial("M_HandProxy_Glove", new Color(0.10f, 0.11f, 0.12f), 0.0f, 0.30f);
            var lensMat = CreateMaterial("M_HandProxy_Lens", new Color(0.85f, 0.92f, 1.0f), 0.2f, 0.95f);
            lensMat.EnableKeyword("_EMISSION");
            lensMat.SetColor("_EmissionColor", new Color(0.7f, 0.85f, 1f) * 0.6f);

            // 前臂
            var forearm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            forearm.name = "Forearm";
            forearm.transform.SetParent(root, false);
            forearm.transform.localPosition = new Vector3(0.03f, -0.07f, -0.16f);
            forearm.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
            forearm.transform.localScale = new Vector3(0.075f, 0.16f, 0.075f);
            forearm.GetComponent<Renderer>().sharedMaterial = gloveMat;
            Destroy(forearm.GetComponent<Collider>());

            // 手掌
            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            palm.transform.SetParent(root, false);
            palm.transform.localPosition = new Vector3(0.0f, -0.015f, 0.0f);
            palm.transform.localScale = new Vector3(0.075f, 0.09f, 0.11f);
            palm.GetComponent<Renderer>().sharedMaterial = gloveMat;
            Destroy(palm.GetComponent<Collider>());

            // 手电筒本体
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "FlashlightBody";
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(0f, 0.02f, 0.12f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.042f, 0.11f, 0.042f);
            body.GetComponent<Renderer>().sharedMaterial = metalMat;
            Destroy(body.GetComponent<Collider>());

            // 透镜
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = "Lens";
            lens.transform.SetParent(root, false);
            lens.transform.localPosition = new Vector3(0f, 0.02f, 0.235f);
            lens.transform.localScale = new Vector3(0.055f, 0.055f, 0.02f);
            lens.GetComponent<Renderer>().sharedMaterial = lensMat;
            Destroy(lens.GetComponent<Collider>());

            // 光源（关键：挂在这里，手电筒才有视觉来源）
            var lightGo = new GameObject("SpotLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.02f, 0.25f);
            lightGo.transform.localRotation = Quaternion.identity;
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 30f;
            spot.range = 22f;
            spot.intensity = 0f;
            spot.color = new Color(0.92f, 0.96f, 1f);
            spot.shadows = LightShadows.Soft;
            spot.enabled = false;

            return root;
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}