﻿using System.Collections;
using UnityEngine;

public class CannonHandle : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private ProjectileWeapon m_Weapon;
	[SerializeField] private float m_RotationSpeed = 100f;
	[SerializeField] private int m_StartingAmmo = 3;
    [Header("References")]
    [SerializeField] private Transform m_YPivot;
    [SerializeField] private Transform m_XPivot;
    [SerializeField] private Transform m_CannonTip;

	private int m_Ammo;

	void Awake()
	{
		m_Ammo = m_StartingAmmo;
	}

    void OnTriggerStay(Collider other)
    {
        if (Input.GetButtonDown("Fire1"))
        {
            StartCoroutine(DragHandle());
        }
    }

    IEnumerator DragHandle()
    {
        var lastHandPosition = VRPlayer.rightHand.position;
        VRPlayer.handsBusy = true;
        while (!Input.GetButtonUp("Fire1"))
        {
            m_YPivot.Rotate(Vector3.up, (lastHandPosition - VRPlayer.rightHand.position).x * m_RotationSpeed);
            m_XPivot.Rotate(Vector3.left, (lastHandPosition - VRPlayer.rightHand.position).y * m_RotationSpeed);
            lastHandPosition = VRPlayer.rightHand.position;
            if (VRPlayer.fire)
            {
				AttemptFire();
            }
            yield return null;
        }
        VRPlayer.handsBusy = false;
    }

	void AttemptFire()
	{
		if (m_Ammo > 0)
		{
			m_Ammo--;
			m_Weapon.Fire(m_CannonTip.position, m_CannonTip.forward);
		}
		else
		{
			AudioManager.outOfAmmo.Play();
		}
	}
}
