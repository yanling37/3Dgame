using UnityEngine;

namespace Game.Jianglin
{
    public class JianglinHealth : MonoBehaviour
    {
        [SerializeField] float maxHp = 80f;
        [SerializeField] bool player;

        float _hp;
        float _shield;
        float _shieldUntil;
        float _reviveAt;
        bool _dead;

        public float MaxHp => maxHp;
        public float Hp => _hp;
        public float Shield => Time.time < _shieldUntil ? _shield : 0f;
        public bool IsPlayer => player;
        public bool IsDead => _dead;
        public Vector3 AimPoint => transform.position + Vector3.up * (player ? 1.2f : 1.05f);

        public void Configure(float hp, bool isPlayer)
        {
            maxHp = Mathf.Max(1f, hp);
            player = isPlayer;
            _hp = maxHp;
            _dead = false;
            _shield = 0f;
        }

        public void AddShield(float amount, float duration)
        {
            _shield = Mathf.Max(_shield, amount);
            _shieldUntil = Time.time + Mathf.Max(0.1f, duration);
        }

        public bool Hurt(float amount)
        {
            if (_dead || amount <= 0f)
            {
                return false;
            }

            if (Time.time < _shieldUntil && _shield > 0f)
            {
                float absorbed = Mathf.Min(_shield, amount);
                _shield -= absorbed;
                amount -= absorbed;
                if (_shield <= 0.01f)
                {
                    _shieldUntil = 0f;
                }
            }

            if (amount <= 0f)
            {
                return false;
            }

            _hp -= amount;
            if (_hp > 0f)
            {
                return false;
            }

            _hp = 0f;
            _dead = true;
            if (player)
            {
                _reviveAt = Time.time + 2.4f;
            }

            return true;
        }

        void Update()
        {
            if (player && _dead && _reviveAt > 0f && Time.time >= _reviveAt)
            {
                ReviveFull();
            }
        }

        public void ReviveFull()
        {
            _hp = maxHp;
            _dead = false;
            _shield = 0f;
            _shieldUntil = 0f;
            _reviveAt = 0f;
        }
    }
}
