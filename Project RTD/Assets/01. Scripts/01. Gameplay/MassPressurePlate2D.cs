using System.Collections.Generic;
using ProjectRTD.Core;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MassPressurePlate2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float requiredMass = 2.5f;
        [SerializeField, Min(0f)] private float doorSpeed = 5f;
        [SerializeField] private Transform door;
        [SerializeField] private Vector3 closedDoorPosition;
        [SerializeField] private Vector3 openDoorPosition;

        private readonly HashSet<Rigidbody2D> bodies = new();
        private SpriteRenderer plateRenderer;

        public float CurrentMass { get; private set; }
        public bool IsPressed => CurrentMass >= requiredMass;

        public void Configure(
            Transform doorTransform,
            Vector3 closedPosition,
            Vector3 openPosition,
            float massThreshold)
        {
            door = doorTransform;
            closedDoorPosition = closedPosition;
            openDoorPosition = openPosition;
            requiredMass = Mathf.Max(0f, massThreshold);
        }

        private void Awake()
        {
            plateRenderer = GetComponent<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            bodies.RemoveWhere(body => body == null || !body.simulated);

            CurrentMass = 0f;
            foreach (Rigidbody2D body in bodies)
            {
                CurrentMass += body.mass;
            }

            if (plateRenderer != null)
            {
                plateRenderer.color = IsPressed ? PrototypePalette.Goal : PrototypePalette.Danger;
            }

            if (door != null)
            {
                Vector3 target = IsPressed ? openDoorPosition : closedDoorPosition;
                door.position = Vector3.MoveTowards(door.position, target, doorSpeed * Time.fixedDeltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            {
                bodies.Add(body);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body != null)
            {
                bodies.Remove(body);
            }
        }
    }
}
