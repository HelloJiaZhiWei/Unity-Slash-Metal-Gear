﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowObject : MonoBehaviour
{

    public Transform target;
    void Update()
    {
        transform.position = target.position;
    }
}
