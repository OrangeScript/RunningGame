using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatTextDisplay : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private Enemy enemy;

    [SerializeField]
    private StatusController statusController;

    [SerializeField]
    private CombatTextPopup popupPrefab;

    [SerializeField]
    private Transform popupAnchor;


    [Header("Layout")]

    [SerializeField]
    private float verticalSpacing = 0.3f;

    [SerializeField]
    private float resetDelay = 0.25f;


    private int currentStackIndex;

    private float lastSpawnTime;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        statusController = GetComponent<StatusController>();
    }

    void OnEnable()
    {
        if(enemy != null)
        {
            enemy.Damaged += HandleDamaged;
        }

        if (statusController != null)
        {
            statusController.ReactionTriggered +=
                HandleReaction;
        }
    }

     private void OnDisable()
    {
        if (enemy != null)
        {
            enemy.Damaged -=
                HandleDamaged;
        }

        if (statusController != null)
        {
            statusController.ReactionTriggered -=
                HandleReaction;
        }
    }

    

    private void HandleDamaged(
        float damage,
        ElementType element
    )
    {
        SpawnText(
            damage.ToString("0.#"),
            GetElementColor(element)
        );
    }


    private void SpawnText(
        string content,
        Color color
    )
    {
        if (
            popupPrefab == null ||
            popupAnchor == null
        )
        {
            return;
        }


        // 隔了一段时间以后重新从最下面开始
        if (
            Time.time -
            lastSpawnTime >
            resetDelay
        )
        {
            currentStackIndex = 0;
        }


        Vector3 spawnPosition =
            popupAnchor.position
            +
            Vector3.up *
            verticalSpacing *
            currentStackIndex;


        CombatTextPopup popup =
            Instantiate(
                popupPrefab,
                spawnPosition,
                Quaternion.identity
            );


        popup.Setup(
            content,
            color
        );


        currentStackIndex++;

        lastSpawnTime =
            Time.time;
    }
    private void HandleReaction(
        ElementReactionType reaction
    )
    {
        string text =
            GetReactionText(
                reaction
            );


        Color color =
            GetReactionColor(
                reaction
            );


        SpawnText(
            text,
            color
        );
    }

    private Color GetReactionColor(
        ElementReactionType reaction
    )
    {
        switch (reaction)
        {
            case ElementReactionType.Frozen:
                return Color.cyan;

            case ElementReactionType.Melt:
                return new Color(
                    1f,
                    0.55f,
                    0.2f
                );

            case ElementReactionType.Vaporize:
                return new Color(
                    0.3f,
                    0.7f,
                    1f
                );

            case ElementReactionType.Overload:
                return new Color(
                    1f,
                    0.35f,
                    0.65f
                );

            case ElementReactionType.ElectroCharged:
                return new Color(
                    0.7f,
                    0.4f,
                    1f
                );
            case ElementReactionType.SuperConduct:
                return new Color(
                    1.5f,
                    0.4f,
                    1f
                );

            default:
                return Color.white;
        }
    }

    private Color GetElementColor(ElementType element)
    {
        return Color.white;
    }


    private string GetReactionText(
        ElementReactionType reaction
    )
    {
        switch (reaction)
        {
            case ElementReactionType.Frozen:
                return "冻结";

            case ElementReactionType.Melt:
                return "融化";

            case ElementReactionType.Vaporize:
                return "蒸发";

            case ElementReactionType.Overload:
                return "超载";

            case ElementReactionType.ElectroCharged:
                return "感电";

            default:
                return reaction.ToString();
        }
    }
}
