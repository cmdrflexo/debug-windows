/*
 * Projects a Gravity Engine body's double-precision position into an SGT floating visual.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class GeSgtBodyView : MonoBehaviour
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private NBody sourceBody;

        [SerializeField]
        private SgtFloatingObject targetVisual;

        private GravityEngine gravityEngine;
        private bool coordinateRangeErrorLogged;

        private void Start()
        {
            gravityEngine = GravityEngine.Instance();
        }

        private void LateUpdate()
        {
            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup() ||
                universeFrame == null ||
                sourceBody == null ||
                targetVisual == null)
            {
                return;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(sourceBody);
            var sceneScale = gravityEngine.GetPhysicalScale();
            var frameOrigin = universeFrame.FrameOrigin;

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    frameOrigin,
                    physicsPosition.x * sceneScale,
                    physicsPosition.y * sceneScale,
                    physicsPosition.z * sceneScale,
                    out var visualPosition))
            {
                if (!coordinateRangeErrorLogged)
                {
                    Debug.LogError(
                        "Cannot project the GE body because its universe position is outside SGT's coordinate range.",
                        this);
                    coordinateRangeErrorLogged = true;
                }

                return;
            }

            coordinateRangeErrorLogged = false;
            targetVisual.SetPosition(visualPosition);
            targetVisual.ApplyPosition();
        }
    }
}
