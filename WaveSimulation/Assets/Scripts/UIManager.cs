using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class WaveSystemConfigs
{
    [Header("Referências de UI")]
    public TMP_InputField amplitudeField;
    public Slider directionSlider;
    public TMP_InputField IntensityField;
}

// Sistema de UI feito na pressa sem seguir bons padrões de projeto
public class UIManager : MonoBehaviour
{
    [Header("Configurações de UI")]
    public Slider resolutionSlider;
    public TMP_InputField lengthInput;
    public Slider lowPassFilterSlider;

    public List<WaveSystemConfigs> configs;
    public GameObject panel;

    [Header("Referências Externas")]

    public WaveSimulation oceanSimulation;
    public GameObject windDirections;
    public Vector3 directionPositions1;
    public Vector3 directionPositions2;
    public GameObject mainCamera;
    public GameObject secondCamera;



    public void GenerateOcean()
    {
        oceanSimulation.Regenerate(this);
    }

    public void SwitchCamera()
    {
        secondCamera.SetActive(mainCamera.activeSelf);
        mainCamera.SetActive(!secondCamera.activeSelf);

        if (mainCamera.activeSelf)
        {
            windDirections.transform.position = directionPositions1;
        }
        else
        {
            windDirections.transform.position = directionPositions2;
        }
    }

    public void setLowPass(float lowPass)
    {
        oceanSimulation.lowPassFilter = lowPass;
    }

    public void ToggleWind()
    {
        windDirections.SetActive(!windDirections.activeSelf);
    }

}
