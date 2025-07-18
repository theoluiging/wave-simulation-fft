using UnityEngine;

public class WindDirectionVisualizer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    Vector3 direction;
    float intensity;
    void Start()
    {
        Vector3 position = lineRenderer.GetPosition(1);
        direction = position.normalized;
        intensity = position.magnitude;
    }
    public void SetIntensity(string input)
    {
        try
        {
            intensity = int.Parse(input);
            lineRenderer.SetPosition(1, direction * intensity);
        }
        catch (System.Exception)
        {
            Debug.Log("Erro ao ler valor de intensidade");
        }
        
    }
    public void SetAngle(float angle)
    {
        direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        lineRenderer.SetPosition(1, direction * intensity);
    }
}
