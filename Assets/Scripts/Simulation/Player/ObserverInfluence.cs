using System;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Player
{
    /// <summary>
    /// Per-region observer nudges. Regions hold independent values.
    /// </summary>
    [Serializable]
    public class RegionObserverInfluence
    {
        [Range(0.5f, 1.5f)] public float FertilityBlessing = 1f;
        [Range(0.5f, 1.5f)] public float HarvestBlessing = 1f;
        /// <summary>Multiplier on disease pressure / disease death (&gt;1 worsens).</summary>
        [Range(0.5f, 1.5f)] public float DiseasePressure = 1f;
        [Range(0.5f, 1.5f)] public float StabilityBlessing = 1f;

        public void ResetSoft()
        {
            FertilityBlessing = 1f;
            HarvestBlessing = 1f;
            DiseasePressure = 1f;
            StabilityBlessing = 1f;
        }

        public void CopyFrom(RegionObserverInfluence other)
        {
            if (other == null)
            {
                return;
            }

            FertilityBlessing = other.FertilityBlessing;
            HarvestBlessing = other.HarvestBlessing;
            DiseasePressure = other.DiseasePressure;
            StabilityBlessing = other.StabilityBlessing;
        }

        public RegionObserverInfluence Clone()
        {
            var copy = new RegionObserverInfluence();
            copy.CopyFrom(this);
            return copy;
        }
    }

    /// <summary>
    /// World-facing observer API for HUD / Phase 1 compatibility.
    /// Underlying storage is region-specific on <see cref="RegionState.Influence"/>.
    /// Focused edits write the focused region; global focus writes all regions equally.
    /// </summary>
    [Serializable]
    public class ObserverInfluence
    {
        public RegionId? FocusRegion;

        /// <summary>Scratch values mirrored from the active focus target for HUD sliders.</summary>
        [Range(0.5f, 1.5f)] public float FertilityBlessing = 1f;
        [Range(0.5f, 1.5f)] public float HarvestBlessing = 1f;
        [Range(0.5f, 1.5f)] public float DiseaseCurse = 1f;
        [Range(0.5f, 1.5f)] public float StabilityBlessing = 1f;

        WorldState _boundWorld;

        public void Bind(WorldState world)
        {
            _boundWorld = world;
            PullFromFocus();
        }

        public void ResetSoft()
        {
            FertilityBlessing = 1f;
            HarvestBlessing = 1f;
            DiseaseCurse = 1f;
            StabilityBlessing = 1f;
            PushToFocus();
        }

        public RegionObserverInfluence GetRegionInfluence(RegionId regionId)
        {
            var region = FindRegion(regionId);
            return region != null ? region.Influence : new RegionObserverInfluence();
        }

        public void SetRegionInfluence(RegionId regionId, RegionObserverInfluence values)
        {
            var region = FindRegion(regionId);
            if (region == null || values == null)
            {
                return;
            }

            region.Influence.CopyFrom(values);
            if (!FocusRegion.HasValue || FocusRegion.Value == regionId)
            {
                PullFromFocus();
            }
        }

        /// <summary>
        /// Resolve effective influence for a region. Values are region-local (no global bleed in P2-A).
        /// </summary>
        public RegionObserverInfluence Resolve(RegionState region)
        {
            if (region?.Influence == null)
            {
                return new RegionObserverInfluence();
            }

            return region.Influence;
        }

        /// <summary>HUD helper: write current slider fields into the focused region(s).</summary>
        public void PushToFocus()
        {
            if (_boundWorld?.Regions == null)
            {
                return;
            }

            if (FocusRegion.HasValue)
            {
                ApplyToRegion(FindRegion(FocusRegion.Value));
                return;
            }

            foreach (var region in _boundWorld.Regions)
            {
                ApplyToRegion(region);
            }
        }

        /// <summary>HUD helper: load slider fields from the focused region (or average of all).</summary>
        public void PullFromFocus()
        {
            if (_boundWorld?.Regions == null || _boundWorld.Regions.Length == 0)
            {
                return;
            }

            if (FocusRegion.HasValue)
            {
                var region = FindRegion(FocusRegion.Value);
                if (region != null)
                {
                    MirrorFrom(region.Influence);
                }

                return;
            }

            // Global view: show first region values (edits still push to all).
            MirrorFrom(_boundWorld.Regions[0].Influence);
        }

        void ApplyToRegion(RegionState region)
        {
            if (region?.Influence == null)
            {
                return;
            }

            region.Influence.FertilityBlessing = FertilityBlessing;
            region.Influence.HarvestBlessing = HarvestBlessing;
            region.Influence.DiseasePressure = DiseaseCurse;
            region.Influence.StabilityBlessing = StabilityBlessing;
        }

        void MirrorFrom(RegionObserverInfluence source)
        {
            if (source == null)
            {
                return;
            }

            FertilityBlessing = source.FertilityBlessing;
            HarvestBlessing = source.HarvestBlessing;
            DiseaseCurse = source.DiseasePressure;
            StabilityBlessing = source.StabilityBlessing;
        }

        RegionState FindRegion(RegionId id)
        {
            if (_boundWorld?.Regions == null)
            {
                return null;
            }

            for (int i = 0; i < _boundWorld.Regions.Length; i++)
            {
                if (_boundWorld.Regions[i].Id == id)
                {
                    return _boundWorld.Regions[i];
                }
            }

            return null;
        }
    }
}
