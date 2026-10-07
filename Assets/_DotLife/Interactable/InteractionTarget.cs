using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class InteractionTarget : MonoBehaviour
{
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();
    private BasicBehaviourScript nearbyPlayer;
    private Collider area;
    private static readonly Predicate<Collider> InvalidContact = IsInvalidContact;
    public event Action<bool> OnEnteringAndExiting;
    public bool IsPlayerInRange => nearbyPlayer != null && contacts.Count > 0;
    public abstract bool RequiresButton { get; }
    public virtual int Priority => 0;

    public bool IsInRange(BasicBehaviourScript player) => isActiveAndEnabled
        && player != null && player == nearbyPlayer && contacts.Count > 0;
    public abstract bool CanInteract(BasicBehaviourScript player);
    public abstract bool TryInteract(InteractingHandler interactor);

    public float DistanceSquared(Vector3 position)
    {
        if (area == null) area = GetComponent<Collider>();
        Vector3 point = area != null ? area.bounds.center : transform.position;
        return (point - position).sqrMagnitude;
    }

    void OnTriggerEnter(Collider other) => AddContact(other);
    void OnTriggerStay(Collider other) => AddContact(other);

    private void AddContact(Collider other)
    {
        if (!isActiveAndEnabled || other == null || other.isTrigger) return;
        BasicBehaviourScript player = other.GetComponentInParent<BasicBehaviourScript>();
        if (player == null || !player.isActiveAndEnabled
            || (nearbyPlayer != null && nearbyPlayer != player)) return;
        bool wasInside = contacts.Count > 0;
        nearbyPlayer = player;
        contacts.Add(other);
        if (!wasInside && contacts.Count > 0)
        {
            player.OnContactsInvalidated += ClearContacts;
            OnEnteringAndExiting?.Invoke(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!contacts.Remove(other) || contacts.Count > 0) return;
        ClearContacts();
    }

    void LateUpdate()
    {
        if (contacts.Count == 0) return;
        contacts.RemoveWhere(InvalidContact);
        if (nearbyPlayer != null && nearbyPlayer.isActiveAndEnabled && contacts.Count > 0) return;
        ClearContacts();
    }

    private static bool IsInvalidContact(Collider contact) => contact == null
        || !contact.enabled || !contact.gameObject.activeInHierarchy;

    protected virtual void OnDisable()
    {
        ClearContacts();
    }

    private void ClearContacts()
    {
        bool wasInside = contacts.Count > 0 || !ReferenceEquals(nearbyPlayer, null);
        if (!ReferenceEquals(nearbyPlayer, null)) nearbyPlayer.OnContactsInvalidated -= ClearContacts;
        contacts.Clear();
        nearbyPlayer = null;
        if (wasInside) OnEnteringAndExiting?.Invoke(false);
    }
}
