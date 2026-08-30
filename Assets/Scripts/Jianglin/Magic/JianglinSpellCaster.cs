using UnityEngine;

namespace Game.Jianglin
{
    public class JianglinSpellCaster : MonoBehaviour
    {
        const float BeamRange = 16f;

        JianglinCameraController _camera;
        Collider _ownerCollider;
        JianglinHealth _ownerHealth;
        JianglinPlayerController _player;
        JianglinTargeting _targeting;

        GameObject _beam;
        LineRenderer _beamLine;
        JianglinSpellId _beamSpell;
        float _beamDps;
        Color _beamColor;
        GameObject _shieldVisual;
        float _shieldVisualUntil;
        float _autoChannelLeft;

        public bool Channeling => _beam != null;

        public void Bind(
            JianglinCameraController camera,
            Collider ownerCollider,
            JianglinPlayerController player,
            JianglinTargeting targeting)
        {
            _camera = camera;
            _ownerCollider = ownerCollider;
            _player = player;
            _targeting = targeting;
            _ownerHealth = ownerCollider != null
                ? ownerCollider.GetComponent<JianglinHealth>()
                : null;
        }

        void Update()
        {
            if (_autoChannelLeft > 0f)
            {
                TickChannel(Time.deltaTime);
                _autoChannelLeft -= Time.deltaTime;
                if (_autoChannelLeft <= 0f)
                {
                    EndChannel();
                }
            }

            if (_shieldVisual != null)
            {
                if (_ownerHealth == null || _ownerHealth.Shield <= 0.05f || Time.time > _shieldVisualUntil)
                {
                    Destroy(_shieldVisual);
                    _shieldVisual = null;
                }
                else if (_player != null)
                {
                    _shieldVisual.transform.position = _player.transform.position + Vector3.up * 0.95f;
                }
            }
        }

        public bool TryCastBorrowed(JianglinSpellId spellId)
        {
            if (spellId == JianglinSpellId.None || _camera == null)
            {
                return false;
            }

            if (JianglinSpellbook.KindOf(spellId) == JianglinCastKind.Channel)
            {
                BeginChannel(spellId);
                _autoChannelLeft = 0.75f;
                return true;
            }

            return TryCast(spellId);
        }

        public bool TryCast(JianglinSpellId spellId)
        {
            if (spellId == JianglinSpellId.None || _camera == null)
            {
                return false;
            }

            switch (JianglinSpellbook.KindOf(spellId))
            {
                case JianglinCastKind.Channel:
                    return false;
                case JianglinCastKind.Skyfall:
                    return SpawnMeteor();
                case JianglinCastKind.Dash:
                    return DoDash();
                case JianglinCastKind.Shield:
                    return ApplyShield();
                default:
                    JianglinProjectileSpec spec;
                    if (!TryGetSpec(spellId, out spec))
                    {
                        return false;
                    }

                    SpawnProjectile(AimRay(), spec);
                    return true;
            }
        }

        public void BeginChannel(JianglinSpellId spellId)
        {
            _autoChannelLeft = 0f;
            EndChannel();
            if (spellId == JianglinSpellId.FlameStream)
            {
                _beamDps = 22f;
                _beamColor = new Color(1f, 0.32f, 0.08f);
            }
            else if (spellId == JianglinSpellId.WaterStream)
            {
                _beamDps = 16f;
                _beamColor = new Color(0.25f, 0.55f, 1f);
            }
            else
            {
                return;
            }

            _beamSpell = spellId;
            _beam = new GameObject("Jianglin_Beam");
            _beamLine = _beam.AddComponent<LineRenderer>();
            _beamLine.positionCount = 2;
            _beamLine.startWidth = 0.22f;
            _beamLine.endWidth = 0.07f;
            var beamShader = Shader.Find("Unlit/Color");
            if (beamShader == null)
            {
                beamShader = Shader.Find("Sprites/Default");
            }

            _beamLine.material = new Material(beamShader) { color = _beamColor };
            _beamLine.startColor = _beamColor;
            _beamLine.endColor = _beamColor;
            _beamLine.useWorldSpace = true;
        }

        public void TickChannel(float deltaTime)
        {
            if (_beamLine == null)
            {
                return;
            }

            Ray aim = AimRay();
            Vector3 end = aim.origin + aim.direction * BeamRange;
            RaycastHit hit;
            if (Physics.Raycast(aim, out hit, BeamRange))
            {
                end = hit.point;
                var health = hit.collider.GetComponentInParent<JianglinHealth>();
                if (health != null && health != _ownerHealth)
                {
                    health.Hurt(_beamDps * deltaTime);
                }
            }

            _beamLine.SetPosition(0, aim.origin + aim.direction * 0.4f);
            _beamLine.SetPosition(1, end);
        }

        public void EndChannel()
        {
            _autoChannelLeft = 0f;
            if (_beam != null)
            {
                Destroy(_beam);
            }

            _beam = null;
            _beamLine = null;
            _beamSpell = JianglinSpellId.None;
        }

        Ray AimRay()
        {
            if (_camera == null)
            {
                return new Ray(transform.position, Vector3.forward);
            }

            if (_targeting != null && _targeting.Locked != null && !_targeting.Locked.IsDead)
            {
                Vector3 origin = _camera.GetAimRay().origin;
                Vector3 to = _targeting.Locked.AimPoint - origin;
                if (to.sqrMagnitude > 0.01f)
                {
                    return new Ray(origin, to.normalized);
                }
            }

            return _camera.GetAimRay();
        }

        bool SpawnMeteor()
        {
            Ray aim = AimRay();
            Vector3 target = aim.origin + aim.direction * 14f;
            RaycastHit hit;
            if (Physics.Raycast(aim, out hit, 40f))
            {
                target = hit.point;
            }

            if (_targeting != null && _targeting.Locked != null && !_targeting.Locked.IsDead)
            {
                target = _targeting.Locked.transform.position;
            }

            var spec = new JianglinProjectileSpec
            {
                Name = "陨石",
                Shape = PrimitiveType.Sphere,
                Scale = Vector3.one * 0.85f,
                Color = new Color(1f, 0.22f, 0.05f),
                BurstColor = new Color(1f, 0.45f, 0.1f),
                Speed = 18f,
                Life = 3.2f,
                Gravity = 28f,
                BurstScale = 3.2f,
                BurstLife = 0.32f,
                Damage = 38f,
                SplashRadius = 3.4f
            };

            var go = GameObject.CreatePrimitive(spec.Shape);
            go.name = "Jianglin_陨石";
            go.transform.position = target + Vector3.up * 13f;
            go.transform.localScale = spec.Scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateSpellMaterial(spec.Color, spec.Name);
            }

            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var projectile = go.AddComponent<JianglinFireball>();
            Vector3 fall = (target + Vector3.up * 0.2f) - go.transform.position;
            projectile.Launch(fall.normalized, spec, _ownerCollider, _ownerHealth);
            return true;
        }

        bool DoDash()
        {
            if (_player == null)
            {
                return false;
            }

            Vector3 dir = AimRay().direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = _player.transform.forward;
            }

            dir.Normalize();
            const float distance = 8.5f;
            _player.Dash(dir, distance, 0.22f);

            var hitIds = new System.Collections.Generic.HashSet<int>();
            Vector3 start = _player.transform.position + Vector3.up * 0.9f;
            for (int i = 0; i <= 4; i++)
            {
                Vector3 p = start + dir * (distance * i / 4f);
                var hits = Physics.OverlapSphere(p, 1.05f);
                for (int h = 0; h < hits.Length; h++)
                {
                    var health = hits[h].GetComponentInParent<JianglinHealth>();
                    if (health == null || health == _ownerHealth || health.IsDead)
                    {
                        continue;
                    }

                    int id = health.GetInstanceID();
                    if (!hitIds.Add(id))
                    {
                        continue;
                    }

                    health.Hurt(24f);
                }
            }

            return true;
        }

        bool ApplyShield()
        {
            if (_ownerHealth == null)
            {
                return false;
            }

            _ownerHealth.AddShield(55f, 5.5f);
            if (_shieldVisual != null)
            {
                Destroy(_shieldVisual);
            }

            _shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _shieldVisual.name = "Jianglin_岩盾";
            _shieldVisual.transform.localScale = Vector3.one * 2.4f;
            var col = _shieldVisual.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var renderer = _shieldVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = CreateSpellMaterial(new Color(0.72f, 0.62f, 0.28f, 0.35f), "岩盾");
                if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 3f);
                }

                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
                if (mat.HasProperty("_Color"))
                {
                    mat.color = new Color(0.82f, 0.7f, 0.28f, 0.32f);
                }

                renderer.material = mat;
            }

            _shieldVisualUntil = Time.time + 5.5f;
            return true;
        }

        static bool TryGetSpec(JianglinSpellId spellId, out JianglinProjectileSpec spec)
        {
            switch (spellId)
            {
                case JianglinSpellId.Fireball:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "火球",
                        Shape = PrimitiveType.Sphere,
                        Scale = Vector3.one * 0.32f,
                        Color = new Color(1f, 0.28f, 0.08f),
                        BurstColor = new Color(1f, 0.45f, 0.12f),
                        Speed = 22f,
                        Life = 4f,
                        BurstScale = 1.4f,
                        BurstLife = 0.18f,
                        Damage = 28f
                    };
                    return true;
                case JianglinSpellId.Spark:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "小火花",
                        Shape = PrimitiveType.Sphere,
                        Scale = Vector3.one * 0.14f,
                        Color = new Color(1f, 0.78f, 0.18f),
                        BurstColor = new Color(1f, 0.55f, 0.12f),
                        Speed = 14f,
                        Life = 2.4f,
                        BurstScale = 0.55f,
                        BurstLife = 0.12f,
                        Damage = 10f
                    };
                    return true;
                case JianglinSpellId.WaterBolt:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "水弹",
                        Shape = PrimitiveType.Sphere,
                        Scale = Vector3.one * 0.30f,
                        Color = new Color(0.18f, 0.48f, 0.95f),
                        BurstColor = new Color(0.45f, 0.72f, 1f),
                        Speed = 16f,
                        Life = 3.6f,
                        Gravity = 3.5f,
                        BurstScale = 1.15f,
                        BurstLife = 0.22f,
                        Damage = 18f
                    };
                    return true;
                case JianglinSpellId.WindBlade:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "风刃",
                        Shape = PrimitiveType.Cube,
                        Scale = new Vector3(0.08f, 0.42f, 0.72f),
                        Color = new Color(0.55f, 0.95f, 0.62f),
                        BurstColor = new Color(0.78f, 1f, 0.82f),
                        Speed = 34f,
                        Life = 2.2f,
                        BurstScale = 0.7f,
                        BurstLife = 0.1f,
                        Damage = 16f
                    };
                    return true;
                case JianglinSpellId.EarthChunk:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "土块",
                        Shape = PrimitiveType.Cube,
                        Scale = Vector3.one * 0.58f,
                        Color = new Color(0.55f, 0.38f, 0.18f),
                        BurstColor = new Color(0.42f, 0.28f, 0.12f),
                        Speed = 9f,
                        Life = 5f,
                        Gravity = 14f,
                        BurstScale = 1.8f,
                        BurstLife = 0.28f,
                        Damage = 32f
                    };
                    return true;
                case JianglinSpellId.MudMire:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "泥沼",
                        Shape = PrimitiveType.Sphere,
                        Scale = Vector3.one * 0.48f,
                        Color = new Color(0.32f, 0.24f, 0.10f),
                        BurstColor = new Color(0.28f, 0.20f, 0.08f),
                        Speed = 11f,
                        Life = 4.5f,
                        Gravity = 8f,
                        BurstScale = 1.3f,
                        BurstLife = 0.2f,
                        LeaveMire = true,
                        MireRadius = 2.4f,
                        MireDuration = 3.4f,
                        MireSlow = 0.38f,
                        Damage = 14f
                    };
                    return true;
                case JianglinSpellId.IceBolt:
                    spec = new JianglinProjectileSpec
                    {
                        Name = "冰弹",
                        Shape = PrimitiveType.Sphere,
                        Scale = Vector3.one * 0.26f,
                        Color = new Color(0.62f, 0.88f, 1f),
                        BurstColor = new Color(0.85f, 0.96f, 1f),
                        Speed = 24f,
                        Life = 3.4f,
                        BurstScale = 1.05f,
                        BurstLife = 0.16f,
                        Damage = 22f
                    };
                    return true;
                default:
                    spec = default;
                    return false;
            }
        }

        void SpawnProjectile(Ray aim, JianglinProjectileSpec spec)
        {
            var go = GameObject.CreatePrimitive(spec.Shape);
            go.name = "Jianglin_" + spec.Name;
            go.transform.position = aim.origin + aim.direction * 1.15f;
            go.transform.localScale = spec.Scale.sqrMagnitude > 0.0001f ? spec.Scale : Vector3.one * 0.3f;
            go.transform.rotation = Quaternion.LookRotation(aim.direction);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateSpellMaterial(spec.Color, spec.Name);
            }

            var collider = go.GetComponent<Collider>();
            collider.isTrigger = true;

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var projectile = go.AddComponent<JianglinFireball>();
            projectile.Launch(aim.direction, spec, _ownerCollider, _ownerHealth);
        }

        public static Material CreateSpellMaterial(Color color, string label)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader)
            {
                name = "Jianglin_" + label,
                color = color
            };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.8f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.55f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.05f);
            }

            return material;
        }
    }
}
