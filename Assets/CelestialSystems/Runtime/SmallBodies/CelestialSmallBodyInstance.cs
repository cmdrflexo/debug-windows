/*
 * A persistent pooled small-body container. Its generated child renderers are
 * accumulated by LOD and exposed through one Unity LODGroup.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyInstance :
        MonoBehaviour
    {
        [Serializable]
        private sealed class LodRepresentation
        {
            [SerializeField]
            private CelestialSmallBodyLod lod;

            [SerializeField]
            private GameObject representation;

            public CelestialSmallBodyLod Lod =>
                lod;

            public GameObject Representation =>
                representation;

            public LodRepresentation(
                CelestialSmallBodyLod newLod,
                GameObject newRepresentation)
            {
                lod = newLod;
                representation = newRepresentation;
            }
        }

        [SerializeField]
        private string sourceToolId;

        [SerializeField]
        private uint seed;

        [SerializeField]
        private bool isPoolManaged;

        [SerializeField]
        private LODGroup lodGroup;

        [Header("LOD Transitions")]
        [SerializeField]
        [Tooltip("Uses Unity dithered cross-fades where the representation shaders support LOD fading.")]
        private bool useCrossFade = true;

        [SerializeField]
        [Range(0.01f, 1.0f)]
        [Tooltip("Fraction of each LOD range reserved for its cross-fade.")]
        private float crossFadeTransitionWidth = 0.20f;

        [SerializeField]
        private List<LodRepresentation> lodRepresentations =
            new List<LodRepresentation>();

        public string SourceToolId =>
            sourceToolId;

        public uint Seed =>
            seed;

        public bool IsPoolManaged =>
            isPoolManaged;

        public int MostDetailedReadyLod
        {
            get
            {
                var mostDetailed =
                    int.MaxValue;

                foreach (var representation in
                    lodRepresentations)
                {
                    if (representation != null &&
                        representation.Representation != null)
                    {
                        mostDetailed =
                            Mathf.Min(
                                mostDetailed,
                                (int)representation.Lod);
                    }
                }

                return mostDetailed;
            }
        }

        internal void ConfigurePoolMetadata(
            string toolId,
            uint newSeed)
        {
            sourceToolId =
                toolId ?? string.Empty;
            seed =
                newSeed;
            isPoolManaged = true;
        }

        internal bool HasLod(
            CelestialSmallBodyLod lod)
        {
            foreach (var representation in
                lodRepresentations)
            {
                if (representation != null &&
                    representation.Lod == lod &&
                    representation.Representation != null)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool HasLodOrHigherDetail(
            CelestialSmallBodyLod lod)
        {
            return MostDetailedReadyLod <=
                (int)lod;
        }

        internal bool TryAddLodRepresentation(
            CelestialSmallBodyLod lod,
            GameObject representation)
        {
            if (representation == null ||
                HasLod(
                    lod))
            {
                return false;
            }

            representation.name =
                $"LOD {(int)lod}";
            representation.transform.SetParent(
                transform,
                false);
            representation.transform.localPosition =
                Vector3.zero;
            representation.transform.localRotation =
                Quaternion.identity;

            lodRepresentations.Add(
                new LodRepresentation(
                    lod,
                    representation));
            RebuildLodGroup();
            return true;
        }

        internal void ClearPoolMetadata()
        {
            isPoolManaged = false;
        }

        private void Reset()
        {
            lodGroup =
                GetComponent<LODGroup>();
        }

        private void RebuildLodGroup()
        {
            lodGroup ??=
                GetComponent<LODGroup>();

            if (lodGroup == null)
            {
                lodGroup =
                    gameObject.AddComponent<
                        LODGroup>();
            }

            lodRepresentations.RemoveAll(
                representation =>
                    representation == null ||
                    representation.Representation == null);

            lodRepresentations.Sort(
                (left, right) =>
                    ((int)left.Lod).CompareTo(
                        (int)right.Lod));

            var lods =
                new List<LOD>();

            foreach (var representation in
                lodRepresentations)
            {
                var renderers =
                    representation.Representation
                        .GetComponentsInChildren<
                            Renderer>(true);

                if (renderers == null ||
                    renderers.Length == 0)
                {
                    continue;
                }

                var unityLod =
                    new LOD(
                        GetTransitionHeight(
                            representation.Lod),
                        renderers)
                    {
                        fadeTransitionWidth =
                            crossFadeTransitionWidth
                    };
                lods.Add(
                    unityLod);
            }

            crossFadeTransitionWidth =
                Mathf.Clamp(
                    crossFadeTransitionWidth,
                    0.01f,
                    1.0f);
            lodGroup.fadeMode =
                useCrossFade
                    ? LODFadeMode.CrossFade
                    : LODFadeMode.None;
            lodGroup.animateCrossFading =
                useCrossFade;
            lodGroup.SetLODs(
                lods.ToArray());
            lodGroup.RecalculateBounds();
        }

        private static float GetTransitionHeight(
            CelestialSmallBodyLod lod)
        {
            return lod switch
            {
                CelestialSmallBodyLod.Lod0 =>
                    0.60f,
                CelestialSmallBodyLod.Lod1 =>
                    0.30f,
                CelestialSmallBodyLod.Lod2 =>
                    0.15f,
                CelestialSmallBodyLod.Lod3 =>
                    0.05f,
                _ => 0.01f
            };
        }
    }
}
