using UnityEngine;

namespace AbyssEcho.Monster
{
    /// <summary>
    /// 怪物激活触发器：玩家进入区域时激活怪物。
    ///
    /// ⚠️ 本类原本写在 Editor 目录里（MonsterSetupBuilder.cs 内），
    ///   导致运行时脚本（AutoTest_S5Monster）无法引用它。
    ///   现移至运行时程序集，Editor 与 Runtime 都能使用。
    ///
    /// 为什么需要延迟激活：
    ///   玩家在 A 区（入口）时，怪物不应该在 B 区游荡消耗性能，
    ///   而且提前相遇会破坏"先探索、后遇敌"的节奏。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MonsterActivationTrigger : MonoBehaviour
    {
        [Tooltip("要激活的怪物")]
        public MonsterAI monster;

        [Tooltip("是否只触发一次")]
        public bool oneShot = true;

        private bool _fired;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired && oneShot) return;
            if (other.GetComponentInParent<AbyssEcho.Player.PlayerMotor>() == null) return;

            _fired = true;
            if (monster != null)
            {
                monster.Activate();
                Debug.Log("[Monster] 已被激活（玩家进入区域）");
            }
        }

        /// <summary>重置触发器。</summary>
        public void ResetTrigger() => _fired = false;
    }
}