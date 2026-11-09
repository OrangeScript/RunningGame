using System;
using System.Collections.Generic;
using UnityEngine;

namespace Running.Game.Run.Generation
{
    public static class DeterministicSectionPlanner
    {
        private static readonly global::SectionType[] CoreTypes =
        {
            global::SectionType.Run,
            global::SectionType.Gate,
            global::SectionType.Combat
        };

        public static SectionRunPlan Generate(
            int seed,
            int requestedLaneCount,
            Vector3 baseOrigin,
            Quaternion baseRotation,
            float laneSpacing,
            int bodySectionCount,
            bool appendBoss,
            bool appendFinish,
            IReadOnlyList<SectionCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new ArgumentException(
                    "At least one section candidate is required.",
                    nameof(candidates));
            }

            int laneCount = Mathf.Clamp(requestedLaneCount, 1, 4);
            int requestedBodyCount = Math.Max(0, bodySectionCount);
            bool hasAllCoreTypes = HasType(candidates, global::SectionType.Run) &&
                                   HasType(candidates, global::SectionType.Gate) &&
                                   HasType(candidates, global::SectionType.Combat);

            // Three slots are the mathematical minimum for guaranteeing that
            // Run/Gate/Combat all appear when the complete core catalog exists.
            int effectiveBodyCount = hasAllCoreTypes
                ? Math.Max(3, requestedBodyCount)
                : requestedBodyCount;

            var lanes = new List<LaneRunPlan>(laneCount);
            Vector3 laneAxis = baseRotation * Vector3.right;

            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                int laneSeed = DeterministicRandom.DeriveSeed(seed, laneIndex + 1);
                var random = new DeterministicRandom(laneSeed);
                List<global::SectionType> bodyTypes = BuildBodyTypeSchedule(
                    effectiveBodyCount,
                    candidates,
                    ref random);

                var plannedSections = new List<PlannedSection>(
                    bodyTypes.Count + (appendBoss ? 1 : 0) + (appendFinish ? 1 : 0));

                for (int i = 0; i < bodyTypes.Count; i++)
                {
                    AddWeightedSection(
                        bodyTypes[i],
                        candidates,
                        ref random,
                        plannedSections);
                }

                if (appendBoss && HasType(candidates, global::SectionType.Boss))
                {
                    AddWeightedSection(
                        global::SectionType.Boss,
                        candidates,
                        ref random,
                        plannedSections);
                }

                if (appendFinish && HasType(candidates, global::SectionType.Finish))
                {
                    AddWeightedSection(
                        global::SectionType.Finish,
                        candidates,
                        ref random,
                        plannedSections);
                }

                float centeredLaneOffset =
                    (laneIndex - (laneCount - 1) * 0.5f) * laneSpacing;
                Vector3 laneOrigin = baseOrigin + laneAxis * centeredLaneOffset;

                lanes.Add(new LaneRunPlan(
                    laneIndex,
                    laneSeed,
                    laneOrigin,
                    baseRotation,
                    plannedSections));
            }

            return new SectionRunPlan(
                seed,
                baseOrigin,
                baseRotation,
                laneSpacing,
                lanes);
        }

        private static List<global::SectionType> BuildBodyTypeSchedule(
            int count,
            IReadOnlyList<SectionCandidate> candidates,
            ref DeterministicRandom random)
        {
            var result = new List<global::SectionType>(count);
            if (count <= 0)
            {
                return result;
            }

            bool hasRun = HasType(candidates, global::SectionType.Run);
            bool hasGate = HasType(candidates, global::SectionType.Gate);
            bool hasCombat = HasType(candidates, global::SectionType.Combat);

            if (hasRun && hasGate && hasCombat)
            {
                // Always give the player one readable section before the first
                // choice or encounter, then use a constrained random schedule.
                // Every core type must appear and identical neighbours are
                // forbidden, but the amount and placement of Run/Gate/Combat
                // are allowed to vary with the lane seed.
                var missing = new HashSet<global::SectionType>(CoreTypes);
                global::SectionType previous = global::SectionType.Run;
                result.Add(previous);
                missing.Remove(previous);

                for (int i = 1; i < count; i++)
                {
                    int slotsAfterThis = count - i - 1;
                    global::SectionType selected = PickConstrainedCoreType(
                        previous,
                        missing,
                        slotsAfterThis,
                        candidates,
                        ref random);
                    result.Add(selected);
                    missing.Remove(selected);
                    previous = selected;
                }

                return result;
            }

            // Degraded catalogs still alternate Run and the available challenge
            // type. This is the compatibility path for today's Run/Gate prefabs.
            if (hasRun && (hasGate || hasCombat))
            {
                global::SectionType challenge = hasGate
                    ? global::SectionType.Gate
                    : global::SectionType.Combat;

                for (int i = 0; i < count; i++)
                {
                    result.Add((i & 1) == 0
                        ? global::SectionType.Run
                        : challenge);
                }

                return result;
            }

            BuildBestEffortSchedule(count, candidates, ref random, result);
            return result;
        }

        private static void BuildBestEffortSchedule(
            int count,
            IReadOnlyList<SectionCandidate> candidates,
            ref DeterministicRandom random,
            List<global::SectionType> result)
        {
            var bodyTypes = new List<global::SectionType>();
            for (int i = 0; i < candidates.Count; i++)
            {
                global::SectionType type = candidates[i].Type;
                if (type == global::SectionType.Boss ||
                    type == global::SectionType.Finish ||
                    bodyTypes.Contains(type))
                {
                    continue;
                }

                bodyTypes.Add(type);
            }

            if (bodyTypes.Count == 0)
            {
                return;
            }

            global::SectionType? previous = null;
            for (int i = 0; i < count; i++)
            {
                global::SectionType selected = PickWeightedType(
                    bodyTypes,
                    previous,
                    candidates,
                    ref random);
                result.Add(selected);
                previous = selected;
            }
        }

        private static global::SectionType PickConstrainedCoreType(
            global::SectionType previous,
            HashSet<global::SectionType> missing,
            int slotsAfterThis,
            IReadOnlyList<SectionCandidate> candidates,
            ref DeterministicRandom random)
        {
            var allowed = new List<global::SectionType>(CoreTypes.Length);
            double total = 0d;

            for (int i = 0; i < CoreTypes.Length; i++)
            {
                global::SectionType type = CoreTypes[i];
                if (type == previous || !HasType(candidates, type))
                    continue;

                int missingAfterSelection = missing.Count - (missing.Contains(type) ? 1 : 0);
                if (missingAfterSelection > slotsAfterThis)
                    continue;

                allowed.Add(type);
                total += SumWeight(candidates, type);
            }

            // The feasibility rule above should always leave a candidate for a
            // complete core catalog. Keeping a deterministic fallback makes a
            // malformed future catalog fail soft instead of truncating a run.
            if (allowed.Count == 0)
            {
                for (int i = 0; i < CoreTypes.Length; i++)
                {
                    if (CoreTypes[i] != previous && HasType(candidates, CoreTypes[i]))
                        return CoreTypes[i];
                }

                return previous;
            }

            if (total <= 0d)
                return allowed[random.NextInt(allowed.Count)];

            double roll = random.NextUnitDouble() * total;
            global::SectionType fallback = allowed[allowed.Count - 1];
            for (int i = 0; i < allowed.Count; i++)
            {
                global::SectionType type = allowed[i];
                roll -= SumWeight(candidates, type);
                if (roll <= 0d)
                    return type;
            }

            return fallback;
        }

        private static global::SectionType PickWeightedType(
            IReadOnlyList<global::SectionType> types,
            global::SectionType? excluded,
            IReadOnlyList<SectionCandidate> candidates,
            ref DeterministicRandom random)
        {
            double total = 0d;
            for (int i = 0; i < types.Count; i++)
            {
                if (types.Count > 1 && excluded.HasValue && types[i] == excluded.Value)
                {
                    continue;
                }

                total += SumWeight(candidates, types[i]);
            }

            if (total <= 0d)
            {
                for (int i = 0; i < types.Count; i++)
                {
                    if (!excluded.HasValue || types.Count == 1 || types[i] != excluded.Value)
                    {
                        return types[i];
                    }
                }
            }

            double roll = random.NextUnitDouble() * total;
            global::SectionType fallback = types[0];
            for (int i = 0; i < types.Count; i++)
            {
                global::SectionType type = types[i];
                if (types.Count > 1 && excluded.HasValue && type == excluded.Value)
                {
                    continue;
                }

                fallback = type;
                roll -= SumWeight(candidates, type);
                if (roll <= 0d)
                {
                    return type;
                }
            }

            return fallback;
        }

        private static void AddWeightedSection(
            global::SectionType type,
            IReadOnlyList<SectionCandidate> candidates,
            ref DeterministicRandom random,
            List<PlannedSection> destination)
        {
            SectionCandidate selected = PickWeightedCandidate(
                type,
                candidates,
                ref random);
            if (selected == null)
            {
                return;
            }

            destination.Add(new PlannedSection(
                destination.Count,
                random.NextSeed(),
                selected.DefinitionId,
                selected.Type,
                selected.Prefab));
        }

        private static SectionCandidate PickWeightedCandidate(
            global::SectionType type,
            IReadOnlyList<SectionCandidate> candidates,
            ref DeterministicRandom random)
        {
            double total = SumWeight(candidates, type);
            if (total <= 0d)
            {
                return null;
            }

            double roll = random.NextUnitDouble() * total;
            SectionCandidate fallback = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                SectionCandidate candidate = candidates[i];
                if (candidate.Type != type || candidate.Weight <= 0f)
                {
                    continue;
                }

                fallback = candidate;
                roll -= candidate.Weight;
                if (roll <= 0d)
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private static bool HasType(
            IReadOnlyList<SectionCandidate> candidates,
            global::SectionType type)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Type == type && candidates[i].Weight > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static double SumWeight(
            IReadOnlyList<SectionCandidate> candidates,
            global::SectionType type)
        {
            double sum = 0d;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Type == type)
                {
                    sum += Math.Max(0f, candidates[i].Weight);
                }
            }

            return sum;
        }
    }
}
