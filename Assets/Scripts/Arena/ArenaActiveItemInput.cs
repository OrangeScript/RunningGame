using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Boss Arena bindings for the two persistent active-item slots.</summary>
[DisallowMultipleComponent]
public sealed class ArenaActiveItemInput : MonoBehaviour
{
    private ItemManager itemManager;
    private NetworkObject networkObject;

    private void Awake()
    {
        itemManager = GetComponent<ItemManager>();
        networkObject = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        if (itemManager == null || networkObject == null || !networkObject.IsOwner)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.qKey.wasPressedThisFrame)
            itemManager.RequestActivateActiveItem(0);
        if (keyboard.eKey.wasPressedThisFrame)
            itemManager.RequestActivateActiveItem(1);
    }
}
