/*
 * Presents the camera-anchor pose library through the generic list-action debug window.
 */

using System.Collections.Generic;
using jcan.DebugWindows;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CameraAnchorPoseDebugWindow : MonoBehaviour
    {
        private const string WindowId = "jcan.celestialsystems.camera-anchor-poses";

        [SerializeField]
        private CameraAnchorPoseLibrary poseLibrary;

        [SerializeField]
        private Vector2 preferredContentSize = new Vector2(360.0f, 260.0f);

        [SerializeField]
        private Vector2 defaultPosition = new Vector2(450.0f, -12.0f);

        private DebugListActionWindow window;
        private bool applicationIsQuitting;

        private void OnEnable()
        {
            applicationIsQuitting = false;

            if (poseLibrary == null)
                poseLibrary = FindFirstObjectByType<CameraAnchorPoseLibrary>();

            if (poseLibrary == null)
            {
                Debug.LogWarning(
                    "CameraAnchorPoseDebugWindow requires a CameraAnchorPoseLibrary.",
                    this);
                return;
            }

            window = new DebugListActionWindow(
                WindowId,
                "Camera Poses",
                new[]
                {
                    new DebugListAction("load", "Load"),
                    new DebugListAction(
                        "delete",
                        "Delete",
                        "DELETED",
                        3.0f,
                        false)
                },
                new DebugListAction("save", "Save", "SAVED"),
                preferredContentSize,
                DebugWindowDisplayState.Open,
                defaultPosition);

            window.ActionInvoked += HandleAction;
            poseLibrary.PosesChanged += RefreshItems;
            window.SetInputText(poseLibrary.CreateDefaultPoseName());
            RefreshItems();

            if (!DebugWindowRegistry.Register(window.CreateRegistration()))
            {
                window.ActionInvoked -= HandleAction;
                poseLibrary.PosesChanged -= RefreshItems;
                window = null;
            }
        }

        private void Start()
        {
            RefreshItems();
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDisable()
        {
            if (window != null)
                window.ActionInvoked -= HandleAction;
            if (poseLibrary != null)
                poseLibrary.PosesChanged -= RefreshItems;

            if (!applicationIsQuitting)
                DebugWindowRegistry.Unregister(WindowId);

            window = null;
        }

        private void HandleAction(DebugListActionInvocation invocation)
        {
            switch (invocation.ActionId)
            {
                case "save":
                    poseLibrary.SaveCurrentPose(invocation.InputText);
                    window.SetInputText(poseLibrary.CreateDefaultPoseName());
                    break;

                case "load":
                    if (invocation.SelectedItem != null)
                        poseLibrary.LoadPose(invocation.SelectedItem.UniqueId);
                    break;

                case "delete":
                    ConfirmDelete(invocation.SelectedItem);
                    break;
            }
        }

        private void ConfirmDelete(DebugListItem selectedItem)
        {
            if (selectedItem == null)
                return;

            var manager = DebugWindowManager.Instance;
            if (manager == null)
                return;

            var poseId = selectedItem.UniqueId;
            var poseName = selectedItem.DisplayName;
            manager.ShowConfirmation(
                $"Delete camera pose '{poseName}'?",
                () =>
                {
                    poseLibrary.DeletePose(poseId);
                    window?.ShowActionFeedback("delete");
                });
        }

        private void RefreshItems()
        {
            if (window == null || poseLibrary == null)
                return;

            var items = new List<DebugListItem>(poseLibrary.Poses.Count);
            for (var i = 0; i < poseLibrary.Poses.Count; i++)
            {
                var pose = poseLibrary.Poses[i];
                items.Add(new DebugListItem(
                    pose.UniqueId,
                    pose.DisplayName));
            }

            window.SetItems(items);
        }
    }
}
