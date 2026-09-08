/*
 * Provides the common receiver binding, contribution lifetime, and cleanup behavior for image-effect sources.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public abstract class ImageEffector : MonoBehaviour
    {
        [SerializeField]
        private ImageEffectorReceiver receiver;

        private string sourceId;

        protected ImageEffectorReceiver Receiver => receiver;
        protected virtual bool ContributionEnabled => true;

        protected virtual void Awake()
        {
            ResolveReceiver();
        }

        protected virtual void OnEnable()
        {
            Apply();
        }

        protected virtual void OnDisable()
        {
            Clear();
        }

        public void BindReceiver(ImageEffectorReceiver value)
        {
            if (receiver == value)
            {
                Apply();
                return;
            }

            Clear();
            receiver = value;
            Apply();
        }

        protected void Apply()
        {
            ResolveReceiver();
            if (receiver == null)
                return;

            if (!isActiveAndEnabled || !ContributionEnabled)
            {
                Clear();
                return;
            }

            receiver.SetContribution(GetSourceId(), BuildContribution());
        }

        protected abstract ImageEffectContribution BuildContribution();

        private void ResolveReceiver()
        {
            if (receiver != null)
                return;

            receiver = GetComponent<ImageEffectorReceiver>();
            if (receiver == null)
                receiver = FindFirstObjectByType<ImageEffectorReceiver>();
        }

        private void Clear()
        {
            receiver?.ClearContribution(GetSourceId());
        }

        private string GetSourceId()
        {
            if (string.IsNullOrEmpty(sourceId))
                sourceId = GetType().FullName + ":" + GetInstanceID();

            return sourceId;
        }
    }
}
