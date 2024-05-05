using UnityEngine;
using System.Collections;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Add this component to an object and it'll impact graviy able characters within its DistanceOfEffect
    /// </summary>
	[AddComponentMenu("Corgi Engine/Environment/Gravity Point")]
	public class GravityPoint : MonoBehaviour 
	{
		public float DistanceOfEffect;

		protected virtual void OnDrawGizmos()
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere (this.transform.position, DistanceOfEffect);
		}
	}
}
