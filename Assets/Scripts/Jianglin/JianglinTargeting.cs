using System.Collections.Generic;
using UnityEngine;

namespace Game.Jianglin
{
    public class JianglinTargeting : MonoBehaviour
    {
        const float MaxLockRange = 28f;

        Transform _player;
        Camera _camera;
        readonly List<JianglinHealth> _buffer = new List<JianglinHealth>(16);

        public JianglinHealth Locked { get; private set; }
        public bool FollowView { get; private set; }

        public void Bind(Transform player, Camera camera)
        {
            _player = player;
            _camera = camera;
        }

        public void ClearIfInvalid()
        {
            if (Locked == null || Locked.IsDead)
            {
                Locked = null;
                FollowView = false;
            }
        }

        public void AdvanceLock()
        {
            ClearIfInvalid();
            if (Locked == null)
            {
                Locked = BestCandidate();
                FollowView = false;
                return;
            }

            if (!FollowView)
            {
                FollowView = true;
                return;
            }

            Locked = null;
            FollowView = false;
        }

        public void Cycle(int direction)
        {
            ClearIfInvalid();
            if (Locked == null)
            {
                Locked = BestCandidate();
                FollowView = false;
                return;
            }

            Collect();
            if (_buffer.Count == 0)
            {
                Locked = null;
                FollowView = false;
                return;
            }

            _buffer.Sort(CompareScreen);
            int index = _buffer.IndexOf(Locked);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + (direction >= 0 ? 1 : _buffer.Count - 1)) % _buffer.Count;
            Locked = _buffer[index];
        }

        JianglinHealth BestCandidate()
        {
            Collect();
            if (_buffer.Count == 0)
            {
                return null;
            }

            _buffer.Sort(CompareScreen);
            return _buffer[0];
        }

        void Collect()
        {
            _buffer.Clear();
            var monsters = FindObjectsOfType<JianglinMonster>();
            Vector3 origin = _player != null ? _player.position : Vector3.zero;
            for (int i = 0; i < monsters.Length; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.Health == null || monster.Health.IsDead)
                {
                    continue;
                }

                if ((monster.transform.position - origin).sqrMagnitude > MaxLockRange * MaxLockRange)
                {
                    continue;
                }

                _buffer.Add(monster.Health);
            }
        }

        int CompareScreen(JianglinHealth a, JianglinHealth b)
        {
            return Score(a).CompareTo(Score(b));
        }

        float Score(JianglinHealth health)
        {
            Vector3 world = health.AimPoint;
            float dist = _player != null ? Vector3.Distance(_player.position, world) : 0f;
            float center = 0f;
            if (_camera != null)
            {
                Vector3 sp = _camera.WorldToScreenPoint(world);
                if (sp.z < 0.1f)
                {
                    center = 2f;
                }
                else
                {
                    float dx = (sp.x / Screen.width) - 0.5f;
                    float dy = (sp.y / Screen.height) - 0.5f;
                    center = dx * dx + dy * dy;
                }
            }

            return center * 18f + dist * 0.04f;
        }
    }
}
