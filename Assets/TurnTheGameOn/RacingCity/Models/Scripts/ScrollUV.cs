using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScrollUV : MonoBehaviour
{
	public int materialIndex = 0;
	public Vector2 uvAnimationRate = new Vector2( 1.0f, 0.0f );
	public string textureName = "_MainTex";
	public Renderer _cachedRenderer;
	Vector2 uvOffset = Vector2.zero;

	void Start () 
    { 
        if (!_cachedRenderer) _cachedRenderer = GetComponent<Renderer>(); 
    }

	void LateUpdate() 
	{
		uvOffset += ( uvAnimationRate * Time.deltaTime );
		if (uvOffset.x > 1) uvOffset.x = uvOffset.x - 1;
		if (uvOffset.y > 1) uvOffset.y = uvOffset.y - 1;
			
		if( _cachedRenderer.enabled )
		{
			_cachedRenderer.materials[ materialIndex ].SetTextureOffset( textureName, uvOffset );
		}
	}
}