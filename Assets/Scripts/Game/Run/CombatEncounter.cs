using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
public class CombatEncounter : MonoBehaviour
{

    [Header("References")]

    [SerializeField]
    private GameManager gameManager;


    [Header("Enemy")]

    [SerializeField]
    private Enemy enemyPrefab;

    [SerializeField]
    private Transform[] spawnPoints;


    [Header("Barriers")]

    [SerializeField]
    private GameObject[] barriers;


    private readonly List<Enemy>
        aliveEnemies =
            new List<Enemy>();


    private bool started;

    private bool cleared;

    private bool finished;


    public bool IsStarted =>
        started;

    public bool IsCleared =>
        cleared;

    public bool IsFinished =>
        finished;

    void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    

    public void FinishEncounter()
    {
        if (finished)
            return;


        finished =
            true;


        SetBarriers(
            false
        );


        Debug.Log(
            $"{name} Finished"
        );
    }


    private void SetBarriers(
        bool active
    )
    {
        foreach (
            GameObject barrier
            in barriers
        )
        {
            if (barrier == null)
                continue;


            barrier.SetActive(
                active
            );
        }
    }


    // ========================================
    // Cleanup
    // ========================================

    private void OnDestroy()
    {
        foreach (
            Enemy enemy
            in aliveEnemies
        )
        {
            if (enemy != null)
            {
                enemy.Died -=
                    HandleEnemyDied;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameManager.instance;
        SetBarriers(false);
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        if (started)
            return;


        RunnerController player =
            other.GetComponentInParent<
                RunnerController
            >();


        if (player == null)
            return;


        BeginEncounter();
    }


    // ========================================
    // Begin
    // ========================================

    public void BeginEncounter()
    {
        if (started)
            return;


        if (gameManager == null)
        {
            Debug.LogError(
                $"{name} 找不到 GameManager"
            );

            return;
        }


        started =
            true;


        Debug.Log(
            $"{name} Begin Encounter"
        );


        SetBarriers(
            true
        );


        gameManager.BeginCombat(
            this
        );


        SpawnEnemies();
    }


    // ========================================
    // Spawn
    // ========================================

    private void SpawnEnemies()
    {
        aliveEnemies.Clear();


        if (enemyPrefab == null)
        {
            Debug.LogError(
                $"{name} 没有配置 Enemy Prefab"
            );

            return;
        }


        foreach (
            Transform spawnPoint
            in spawnPoints
        )
        {
            if (spawnPoint == null)
                continue;


            Enemy enemy =
                Instantiate(
                    enemyPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation
                );


            enemy.Died +=
                HandleEnemyDied;


            aliveEnemies.Add(
                enemy
            );
        }


        Debug.Log(
            $"{name} Spawn Enemy：{aliveEnemies.Count}"
        );


        if (
            aliveEnemies.Count ==
            0
        )
        {
            ClearEncounter();
        }
    }


    // ========================================
    // Death
    // ========================================

    private void HandleEnemyDied(
        Enemy enemy
    )
    {
        if (enemy == null)
            return;


        enemy.Died -=
            HandleEnemyDied;


        aliveEnemies.Remove(
            enemy
        );


        Debug.Log(
            $"{name} Remaining Enemy：{aliveEnemies.Count}"
        );


        if (
            aliveEnemies.Count ==
            0
        )
        {
            ClearEncounter();
        }
    }

    private void ClearEncounter()
    {
        if (cleared)
            return;


        cleared =
            true;


        Debug.Log(
            $"{name} Cleared"
        );


        gameManager.CompleteCombat(
            this
        );
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
