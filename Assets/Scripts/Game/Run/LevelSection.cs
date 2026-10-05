using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum SectionType
{
    Run,
    Gate,
    Combat,
    Elite,
    Boss,
    Finish
}
public class LevelSection : MonoBehaviour
{
    [Header("Section")]
    [SerializeField]
    private SectionType sectionType;

    [SerializeField]
    private Transform exitPoint;
    public SectionType Type => sectionType;
    public Transform ExitPoint => exitPoint;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
