﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketController : MonoBehaviour {

    private GameObject GameFunctions;
    private GameObject GameController;
    public GameObject rocketExplosion;
    public GameObject explosion;
    public float rocketSpeed = 30;

    // Use this for initialization
    void Start () {
        Destroy(this.gameObject, 5);
        GameFunctions = GameObject.FindGameObjectWithTag("GameFunctions");
        GameController = GameObject.FindGameObjectWithTag("GameController");
        GetComponent<Rigidbody>().velocity = GameFunctions.GetComponent<GameFunctions>().FindBulletVelocity(this.gameObject, rocketSpeed);
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy")) {
            GameFunctions.GetComponent<GameFunctions>().RocketExplode(this.gameObject, other.gameObject, rocketExplosion, explosion);
        }
    }
}