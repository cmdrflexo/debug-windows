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
        private SgtGravityOriginBridge originBridge;

        [SerializeField]
        private NBody sourceBody;

        [SerializeField]
        private SgtFloatingObject targetVisual;

        private GravityEngine gravityEngine;

        private void Start()
        {
            gravityEngine = GravityEngine.Instance();
        }

        private void LateUpdate()
        {
            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup() ||
                originBridge == null ||
                sourceBody == null ||
                targetVisual == null)
            {
                return;
            }

            var physicsPosition = gravityEngine.GetPositionDoubleV3(sourceBody);
            var sceneScale = gravityEngine.GetPhysicalScale();
            var frameOrigin = originBridge.FrameOrigin;
            var visualPosition = new SgtPosition
            {
                GlobalX = frameOrigin.CellX,
                GlobalY = frameOrigin.CellY,
                GlobalZ = frameOrigin.CellZ,
                LocalX = frameOrigin.LocalXMeters + physicsPosition.x * sceneScale,
                LocalY = frameOrigin.LocalYMeters + physicsPosition.y * sceneScale,
                LocalZ = frameOrigin.LocalZMeters + physicsPosition.z * sceneScale
            };

            visualPosition.SnapLocal();
            targetVisual.SetPosition(visualPosition);
            targetVisual.ApplyPosition();
        }
    }
}
