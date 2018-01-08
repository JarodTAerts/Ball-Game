﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GrenadePickupController : MonoBehaviour {

    private Text messageText;
    private GameObject GameFunctions;


    // Use this for initialization
    void Start()
    {
        messageText = GameObject.Find("Canvas/MessageText").GetComponent<Text>();
        GameFunctions = GameObject.FindGameObjectWithTag("GameFunctions");
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(new Vector3(45, 30, 15) * Time.deltaTime);
    }

    void OnTriggerStay(Collider other)
    {
        if (other.tag == "Player")
        {
            other.GetComponent<PlayerController>().totalGrenades++;
            GameFunctions.GetComponent<GameFunctions>().UpdateAmmoText();
            Destroy(this.gameObject);
        }
    }


}