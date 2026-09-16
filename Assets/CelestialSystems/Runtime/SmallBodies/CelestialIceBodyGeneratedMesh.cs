/*
 * Releases the runtime mesh created for one generated ice-body instance.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class CelestialIceBodyGeneratedMesh :
        MonoBehaviour
    {
        private Mesh ownedMesh;

        private void Awake()
        {
            var filter =
                GetComponent<MeshFilter>();
            ownedMesh =
                filter != null
                    ? filter.sharedMesh
                    : null;
        }

        private void OnDestroy()
        {
            if (ownedMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(
                    ownedMesh);
            }
            else
            {
                DestroyImmediate(
                    ownedMesh);
            }
        }
    }
}
