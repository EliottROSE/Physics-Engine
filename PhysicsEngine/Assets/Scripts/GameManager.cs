using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    #region Singleton

    static GameManager instance = null;

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<GameManager>();
            return instance;
        }
    }

    #endregion
    
    
    [SerializeField] private Ball BallPrefab;
    [SerializeField] private GameObject spawnPoint;

    [SerializeField] private float Money = 100f;
    [SerializeField] private float CurrentMoneyPerBall = 10f;
    
    [SerializeField] private Button SpawnBallButton;
    [SerializeField] private Button AddMoneyButton;
    [SerializeField] private Button RemoveMoneyButton;
    [SerializeField] private TMP_Text MoneyText;
    
    [SerializeField] private Button DebugButton;

    public void Start()
    {
        SetMoneyText();
    }

    public void ModifyMoney(float multiplier, float ballValue)
    {
        Money += ballValue * multiplier;
        SetMoneyText();
    }
    
    public void OnSpawnBallButtonClick()
    {
        if (Money < CurrentMoneyPerBall)
            return;
        
        Ball newBall = Instantiate(BallPrefab, spawnPoint.transform.position, Quaternion.identity);
        newBall.SetBallValue(CurrentMoneyPerBall);
        PhysicsManager.Instance.AddCollider(newBall.gameObject);
        
        Money -= CurrentMoneyPerBall;
        SetMoneyText();
    }
    
    public void SetMoneyText()
    {
        MoneyText.text = "Money : " + Money;
    }
}
