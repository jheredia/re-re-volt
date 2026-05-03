using Core;
using UnityEngine;

public class RaceTester : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.StartGame();
    }

}
