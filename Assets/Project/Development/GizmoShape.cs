using UnityEngine;

public class GizmoShape : MonoBehaviour {

	public enum GizmoShapeType {
		Cube,
		Sphere
	}

	public jcan.CelestialSystems.GizmoDrawMode drawMode;
	public GizmoShapeType shape;
	public Transform target;
	public Vector3 size = Vector3.one * 100000f;
	public Color color = Color.yellow;

	private void OnDrawGizmos() {
		if(drawMode == jcan.CelestialSystems.GizmoDrawMode.Always)
			DrawGizmos();
	}

	private void OnDrawGizmosSelected() {
		if(drawMode == jcan.CelestialSystems.GizmoDrawMode.Selected)
			DrawGizmos();
	}

	private void DrawGizmos() {
		Vector3 position = target != null ? target.position : Vector3.zero;
		Gizmos.color = color;
		if(shape == GizmoShapeType.Cube)
			Gizmos.DrawWireCube(position, size);
		if(shape == GizmoShapeType.Sphere)
			Gizmos.DrawWireSphere(position, size.x);
	}

}
