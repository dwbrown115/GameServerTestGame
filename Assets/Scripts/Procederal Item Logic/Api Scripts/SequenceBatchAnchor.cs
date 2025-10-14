using UnityEngine;

namespace Game.Procederal.Api
{
    /// Maintains an anchor transform for a sequence batch, positioning it relative to the owner
    /// using a star/polygon distribution around the forward direction.
    [DisallowMultipleComponent]
    public class SequenceBatchAnchor : MonoBehaviour
    {
        [Header("Anchor Settings")]
        public SequenceSpawnBehavior sequence;
        public Transform owner;
        public float radius = 0f;
        public float angleDeg = 0f;
        public bool debugLogs = false;

        public void Configure(
            SequenceSpawnBehavior seq,
            Transform ownerTransform,
            float radiusValue,
            float angleDegrees,
            bool enableDebug
        )
        {
            sequence = seq;
            owner = ownerTransform;
            radius = radiusValue;
            angleDeg = angleDegrees;
            debugLogs = enableDebug;
            ApplyPosition();
        }

        private void LateUpdate()
        {
            ApplyPosition();
        }

        public void ApplyPosition()
        {
            if (sequence == null)
                return;

            Vector2 forward = sequence.PeekForwardDirection();
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector2.right;
            forward.Normalize();
            Vector2 right = new Vector2(-forward.y, forward.x);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 offsetDir = forward * Mathf.Cos(rad) + right * Mathf.Sin(rad);
            Vector2 basePos;
            if (owner != null)
                basePos = owner.position;
            else if (sequence.owner != null)
                basePos = sequence.owner.position;
            else
                basePos = sequence.transform.position;

            Vector2 targetPos = basePos + offsetDir * Mathf.Max(0f, radius);
            var current = transform.position;
            transform.position = new Vector3(targetPos.x, targetPos.y, current.z);

            if (debugLogs)
                Debug.DrawLine(basePos, targetPos, Color.cyan, Time.deltaTime, false);
        }
    }
}
