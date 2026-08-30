using UnityEngine;

namespace Game.Jianglin
{
    /// <summary>
    /// Shared Jianglin projectile. Shape, color, speed and optional mire zone come from
    /// <see cref="JianglinProjectileSpec"/> so test spells do not need duplicate scripts.
    /// </summary>
    public class JianglinFireball : MonoBehaviour
    {
        Vector3 _velocity;
        float _gravity;
        float _life = 4f;
        Color _burstColor = new Color(1f, 0.45f, 0.12f);
        float _burstScale = 1.4f;
        float _burstLife = 0.18f;
        string _burstName = "Jianglin_SpellBurst";
        bool _leaveMire;
        float _mireRadius = 2.2f;
        float _mireDuration = 3.2f;
        float _mireSlow = 0.42f;
        float _damage;
        float _splash;
        Collider _ignore;
        JianglinHealth _ownerHealth;
        bool _exploded;

        public void Launch(Vector3 direction, float speed, Collider ignore)
        {
            Launch(direction, DefaultFireballSpec(speed), ignore, null);
        }

        public void Launch(Vector3 direction, JianglinProjectileSpec spec, Collider ignore)
        {
            Launch(direction, spec, ignore, null);
        }

        public void Launch(Vector3 direction, JianglinProjectileSpec spec, Collider ignore, JianglinHealth ownerHealth)
        {
            Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            _velocity = dir * Mathf.Max(0.1f, spec.Speed);
            _gravity = Mathf.Max(0f, spec.Gravity);
            _life = spec.Life > 0.1f ? spec.Life : 4f;
            _burstColor = spec.BurstColor;
            _burstScale = spec.BurstScale > 0.01f ? spec.BurstScale : 1.2f;
            _burstLife = spec.BurstLife > 0.01f ? spec.BurstLife : 0.18f;
            _burstName = "Jianglin_" + spec.Name + "Burst";
            _leaveMire = spec.LeaveMire;
            _mireRadius = spec.MireRadius;
            _mireDuration = spec.MireDuration;
            _mireSlow = spec.MireSlow;
            _damage = spec.Damage;
            _splash = spec.SplashRadius;
            _ignore = ignore;
            _ownerHealth = ownerHealth;
            if (ignore != null)
            {
                var self = GetComponent<Collider>();
                if (self != null)
                {
                    Physics.IgnoreCollision(self, ignore, true);
                }
            }

            if (spec.Shape != PrimitiveType.Sphere)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        void Update()
        {
            if (_exploded)
            {
                return;
            }

            if (_gravity > 0f)
            {
                _velocity += Vector3.down * _gravity * Time.deltaTime;
            }

            transform.position += _velocity * Time.deltaTime;
            if (_velocity.sqrMagnitude > 0.001f && GetComponent<MeshFilter>() != null)
            {
                transform.rotation = Quaternion.LookRotation(_velocity.normalized);
            }

            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Explode();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (_exploded || other == null || other == _ignore)
            {
                return;
            }

            if (other.GetComponentInParent<JianglinFireball>() != null)
            {
                return;
            }

            var health = other.GetComponentInParent<JianglinHealth>();
            if (health != null && health == _ownerHealth)
            {
                return;
            }

            if (health != null)
            {
                health.Hurt(_damage);
            }

            Explode();
        }

        void Explode()
        {
            if (_exploded)
            {
                return;
            }

            _exploded = true;
            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = _burstName;
            burst.transform.position = transform.position;
            burst.transform.localScale = Vector3.one * _burstScale;
            var collider = burst.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = burst.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = JianglinSpellCaster.CreateSpellMaterial(_burstColor, "爆点");
            }

            Destroy(burst, _burstLife);

            if (_splash > 0.05f)
            {
                var splashHits = Physics.OverlapSphere(transform.position, _splash);
                for (int i = 0; i < splashHits.Length; i++)
                {
                    var health = splashHits[i].GetComponentInParent<JianglinHealth>();
                    if (health != null && health != _ownerHealth && !health.IsDead)
                    {
                        health.Hurt(_damage * 0.55f);
                    }
                }
            }

            if (_leaveMire)
            {
                JianglinSlowZone.Spawn(transform.position, _mireRadius, _mireDuration, _mireSlow);
            }

            Destroy(gameObject);
        }

        static JianglinProjectileSpec DefaultFireballSpec(float speed)
        {
            return new JianglinProjectileSpec
            {
                Name = "火球",
                Shape = PrimitiveType.Sphere,
                Scale = Vector3.one * 0.32f,
                Color = new Color(1f, 0.28f, 0.08f),
                BurstColor = new Color(1f, 0.45f, 0.12f),
                Speed = speed,
                Life = 4f,
                BurstScale = 1.4f,
                BurstLife = 0.18f
            };
        }
    }

    public class JianglinSlowZone : MonoBehaviour
    {
        float _radius = 2.2f;
        float _life = 3.2f;
        float _slow = 0.42f;

        public static void Spawn(Vector3 position, float radius, float duration, float slow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Jianglin_MudMire";
            float r = Mathf.Max(0.6f, radius);
            go.transform.position = position + Vector3.up * 0.04f;
            go.transform.localScale = new Vector3(r * 2f, 0.06f, r * 2f);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = JianglinSpellCaster.CreateSpellMaterial(
                    new Color(0.28f, 0.18f, 0.08f, 0.92f),
                    "泥沼");
            }

            var zone = go.AddComponent<JianglinSlowZone>();
            zone._radius = r;
            zone._life = duration > 0.1f ? duration : 3f;
            zone._slow = Mathf.Clamp(slow, 0.15f, 0.9f);
            Destroy(go, zone._life + 0.05f);
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                return;
            }

            var hits = Physics.OverlapSphere(transform.position, _radius);
            for (int i = 0; i < hits.Length; i++)
            {
                var player = hits[i].GetComponentInParent<JianglinPlayerController>();
                if (player != null)
                {
                    player.SetMoveSlow(_slow, 0.12f);
                }

                var monster = hits[i].GetComponentInParent<JianglinMonster>();
                if (monster != null)
                {
                    monster.SetMoveSlow(_slow, 0.12f);
                }
            }
        }
    }
}
