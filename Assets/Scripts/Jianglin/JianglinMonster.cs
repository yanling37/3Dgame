using UnityEngine;

namespace Game.Jianglin
{
    public enum JianglinMonsterKind
    {
        Grunt,
        Brute
    }

    [RequireComponent(typeof(CharacterController))]
    public class JianglinMonster : MonoBehaviour
    {
        Transform _player;
        CharacterController _controller;
        JianglinHealth _health;
        Vector3 _wander;
        Vector3 _velocity;
        float _attackReady;
        float _wanderAt;
        float _moveSlow = 1f;
        float _moveSlowUntil;
        float _speed = 3.2f;
        float _damage = 8f;
        float _range = 1.7f;
        float _chase = 14f;

        public JianglinHealth Health => _health;

        public void Configure(Transform player, JianglinMonsterKind kind, Vector3 spawn)
        {
            _player = player;
            transform.position = spawn;
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<JianglinHealth>();
            if (kind == JianglinMonsterKind.Brute)
            {
                _speed = 2.1f;
                _damage = 14f;
                _range = 1.9f;
                _chase = 16f;
                if (_health != null)
                {
                    _health.Configure(90f, false);
                }
            }
            else if (_health != null)
            {
                _health.Configure(42f, false);
            }

            PickWander();
        }

        public void SetMoveSlow(float multiplier, float duration)
        {
            _moveSlow = Mathf.Clamp(multiplier, 0.15f, 1f);
            _moveSlowUntil = Mathf.Max(_moveSlowUntil, Time.time + Mathf.Max(0.02f, duration));
        }

        void Update()
        {
            if (_controller == null)
            {
                return;
            }

            if (_health != null && _health.IsDead)
            {
                transform.position += Vector3.down * 1.6f * Time.deltaTime;
                Destroy(gameObject, 0.7f);
                enabled = false;
                return;
            }

            bool grounded = _controller.isGrounded;
            if (grounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }

            Vector3 planar = ChooseMove();
            float slow = Time.time < _moveSlowUntil ? _moveSlow : 1f;
            _controller.Move(planar * _speed * slow * Time.deltaTime);
            _velocity.y += -24f * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);

            if (planar.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(planar),
                    1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            TryMelee();
        }

        Vector3 ChooseMove()
        {
            if (_player == null)
            {
                return Vector3.zero;
            }

            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist < _chase && dist > _range * 0.85f)
            {
                return toPlayer.normalized;
            }

            if (dist <= _range)
            {
                return Vector3.zero;
            }

            if (Time.time >= _wanderAt || (transform.position - _wander).sqrMagnitude < 1.2f)
            {
                PickWander();
            }

            Vector3 toWander = _wander - transform.position;
            toWander.y = 0f;
            return toWander.sqrMagnitude > 0.05f ? toWander.normalized : Vector3.zero;
        }

        void TryMelee()
        {
            if (_player == null || Time.time < _attackReady)
            {
                return;
            }

            Vector3 delta = _player.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude > _range)
            {
                return;
            }

            var hp = _player.GetComponent<JianglinHealth>();
            if (hp != null && !hp.IsDead)
            {
                hp.Hurt(_damage);
            }

            _attackReady = Time.time + 1.15f;
        }

        void PickWander()
        {
            _wanderAt = Time.time + Random.Range(2.2f, 5.5f);
            Vector2 ring = Random.insideUnitCircle * 10f;
            Vector3 origin = _player != null ? _player.position : transform.position;
            _wander = new Vector3(origin.x + ring.x, transform.position.y, origin.z + ring.y);
        }
    }
}
