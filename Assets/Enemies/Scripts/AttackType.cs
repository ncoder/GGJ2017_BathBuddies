﻿using UnityEngine;

[CreateAssetMenu(menuName = "Data/AttackObject")]
public class AttackType : ScriptableObject
{
    public int damage;
    public int range;
    public string originator;
    public GameObject projectile;
    public GameObject fireEffect;
    public GameObject hitEffect;
    public float attackForce;
    public float attackCooldown;
    public SOUNDS attackSound = SOUNDS.NO_SOUND;
    public SOUNDS hitSound = SOUNDS.NO_SOUND;
}
