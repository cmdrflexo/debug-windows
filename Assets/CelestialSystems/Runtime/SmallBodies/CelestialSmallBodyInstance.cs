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

        [SerializeField]
        private List<LodRepresentation> lodRepresentations =
            new List<LodRepresentation>();

        public string SourceToolId =>
            sourceToolId;

        public uint Seed =>
            seed;

        public bool IsPoolManaged =>
            isPoolManaged;

        public int HighestReadyLod
        {
            get
            {
                var highest =
                    -1;

                foreach (var representation in
                    lodRepresentations)
                {
                    if (representation != null &&
                        representation.Representation != null)
                    {
                        highest =
                            Mathf.Max(
                                highest,
                                (int)representation.Lod);
                    }
                }

                return highest;
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

        internal bool HasAtLeastLod(
            CelestialSmallBodyLod lod)
        {
            return HighestReadyLod >=
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
                    ((int)right.Lod).CompareTo(
                        (int)left.Lod));

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

                lods.Add(
                    new LOD(
                        GetTransitionHeight(
                            representation.Lod),
                        renderers));
            }

            lodGroup.SetLODs(
                lods.ToArray());
            lodGroup.RecalculateBounds();
        }

        private static float GetTransitionHeight(
            CelestialSmallBodyLod lod)
        {
            return lod switch
            {
                CelestialSmallBodyLod.Detail4 =>
                    0.60f,
                CelestialSmallBodyLod.Detail3 =>
                    0.30f,
                CelestialSmallBodyLod.Detail2 =>
                    0.15f,
                CelestialSmallBodyLod.Detail1 =>
                    0.05f,
                _ => 0.01f
            };
        }
    }
}
