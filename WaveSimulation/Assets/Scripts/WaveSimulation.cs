using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class WaveSystem
{
    public float amplitude = 10f;
    public Vector2 wind = new Vector2(10.0f, 10.0f);
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaveSimulation : MonoBehaviour
{
    [Header("Configurações das Ondas")]
    public int size = 128; // Tamanho da grade, deve ser uma potência de 2
    public float length = 128; // Tamanho físico do patch de oceano no mundo
    public List<WaveSystem> waveSystems;

    [Header("Configurações do Filtro")]
    [Range(0, 1.0f)]
    public float lowPassFilter = 0.9f; 


    // Componentes e malha
    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] displacedVertices;
    private int[] triangles;

    // Dados da FFT
    private Complex[,] h0; // Espectro de Fourier da altura inicial (em t=0)
    private Complex[,] h_tilde; // Espectro de Fourier da altura em um dado tempo t
    private Complex[,] dx_tilde; // Espectro de Fourier do deslocamento em X
    private Complex[,] dz_tilde; // Espectro de Fourier do deslocamento em Z
    private Complex[,] slope_x_tilde; // Espectro para a inclinação em X
    private Complex[,] slope_z_tilde; // Espectro para a inclinação em Z

    private float g = 9.81f; 
    private float time = 0f;
    private float maxFrequency;

    void Start()
    {
        Init();
    }

    //Método para receber valores do UIManager e reiniciar a simulação
    public void Regenerate(UIManager manager)
    {
        try
        {
            size = (int)Mathf.Pow(2, manager.resolutionSlider.value);
            if (int.Parse(manager.lengthInput.text) < 1)
            {
                throw new System.Exception();
            }
            length = int.Parse(manager.lengthInput.text);
            lowPassFilter = manager.lowPassFilterSlider.value;

            List<WaveSystemConfigs> configs = manager.configs;
            for (int i = 0; i < configs.Count; i++)
            {
                waveSystems[i].amplitude = int.Parse(configs[i].amplitudeField.text);

                float angle = configs[i].directionSlider.value;

                int intensity = int.Parse(configs[i].IntensityField.text);
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                waveSystems[i].wind = intensity * direction;
            }

            Init();
        }
        catch (System.Exception)
        {
            Debug.LogWarning("Erro ao gerar oceano!");
        }
    }

    //Método para iniciar a simulação
    public void Init()
    {
        if (Mathf.IsPowerOfTwo(size) == false)
        {
            Debug.LogError("O tamanho (size) deve ser uma potência de 2!");
            enabled = false;
            return;
        }

        if (mesh != null)
        {
            Destroy(mesh);
        }
        InitializeMesh();
        InitializeFFTData();
        GenerateInitialSpectrum();
    }

    void Update()
    {
        time += Time.deltaTime;
        CalculateWaveHeights(time);
        UpdateMesh();
    }

    // Método para criar a malha proceduralmente
    private void InitializeMesh()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GetComponent<MeshFilter>().mesh = mesh;
        mesh.name = "Oceano Procedural";

        int vertexCount = size * size;
        baseVertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        displacedVertices = new Vector3[vertexCount];
        
        int triangleCount = (size - 1) * (size - 1) * 6;
        triangles = new int[triangleCount];

        // Criar vértices e UVs
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = z * size + x;
                Vector2 percent = new Vector2(x / (float)(size - 1), z / (float)(size - 1));
                baseVertices[i] = new Vector3((percent.x - 0.5f) * length, 0, (percent.y - 0.5f) * length);
                uvs[i] = new Vector2(percent.x, percent.y);
            }
        }
        
        // Criar triângulos com padrão alternado
        int triIndex = 0;
        for (int z = 0; z < size - 1; z++)
        {
            for (int x = 0; x < size - 1; x++)
            {
                int current = z * size + x;
                int next = current + 1;
                int above = current + size;
                int aboveNext = above + 1;

                // Usa um padrão de tabuleiro de xadrez para decidir a direção da diagonal
                if ((x + z) % 2 == 0)
                {
                    // Primeira direção da diagonal
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = above;
                    triangles[triIndex++] = aboveNext;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = aboveNext;
                    triangles[triIndex++] = next;
                }
                else
                {
                    // Segunda direção da diagonal (invertida)
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = above;
                    triangles[triIndex++] = next;

                    triangles[triIndex++] = next;
                    triangles[triIndex++] = above;
                    triangles[triIndex++] = aboveNext;
                }
            }
        }

        mesh.vertices = baseVertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
    }


    // Inicializa os arrays que guardarão os dados da FFT
    private void InitializeFFTData()
    {
        h0 = new Complex[size, size];
        h_tilde = new Complex[size, size];
        dx_tilde = new Complex[size, size];
        dz_tilde = new Complex[size, size];
        slope_x_tilde = new Complex[size, size];
        slope_z_tilde = new Complex[size, size];

        Vector2 k_max = new Vector2(2.0f * Mathf.PI * (size / 2) / length, 2.0f * Mathf.PI * (size / 2) / length);
        maxFrequency = k_max.magnitude;
    }

    // Gera o espectro inicial com base no modelo de Phillips
    private void GenerateInitialSpectrum()
    {
        // Zera o espectro inicial antes de começar
        for (int m = 0; m < size; m++)
        {
            for (int n = 0; n < size; n++)
            {
                h0[n, m] = new Complex(0, 0);
            }
        }
        foreach (var system in waveSystems)
        {
            for (int m = 0; m < size; m++)
            {
                for (int n = 0; n < size; n++)
                {
                    float r1 = Random.value;
                    if (r1 == 0) r1 = 0.0001f; // Evita log(0)
                    float r2 = Random.value;
                    float xi_r = Mathf.Sqrt(-2.0f * Mathf.Log(r1)) * Mathf.Cos(2.0f * Mathf.PI * r2);
                    float xi_i = Mathf.Sqrt(-2.0f * Mathf.Log(r1)) * Mathf.Sin(2.0f * Mathf.PI * r2);
                    Complex xi = new Complex(xi_r, xi_i);

                    // Soma a contribuição deste sistema ao h0 total
                    h0[n, m] += GetPhillipsSpectrum(n, m, system) * xi;
                }
            }
        }
    }

    // Calcula o valor do espectro de Phillips para um dado vetor de onda k
    private Complex GetPhillipsSpectrum(int n, int m, WaveSystem system)
    {
        int n_shifted = (n < size / 2) ? n : n - size;
        int m_shifted = (m < size / 2) ? m : m - size;

        Vector2 k = new Vector2(
            2.0f * Mathf.PI * n_shifted / length,
            2.0f * Mathf.PI * m_shifted / length
        );
        float k_magnitude = k.magnitude;
        if (k_magnitude < 0.0001f)
        {
            return new Complex(0, 0);
        }

        // A escala da maior onda possível, baseada na velocidade do vento V. 
        float L = system.wind.magnitude * system.wind.magnitude / g;
        
        float k_dot_w = Vector2.Dot(k.normalized, system.wind.normalized);

        // Phillips Spectrum (Equação 40) 
        float phillips = system.amplitude 
                        * (Mathf.Exp(-1.0f / (k_magnitude * k_magnitude * L * L)) / Mathf.Pow(k_magnitude, 4)) 
                        * (k_dot_w * k_dot_w);

        // Supressão de ondas muito pequenas (Equação 41)
        // O artigo sugere suprimir ondas menores que um comprimento 'l' (minúsculo).
        // Usar um valor arbitrário, bem menor que L, para este corte.
        float l_small_cutoff = L / 1000.0f; 
        phillips *= Mathf.Exp(-k_magnitude * k_magnitude * l_small_cutoff * l_small_cutoff);
        
        // O resultado é a raiz quadrada da energia do espectro
        return new Complex(0, Mathf.Sqrt(phillips * 0.5f));
    }

    // Calcula a altura das ondas para o tempo atual
    private void CalculateWaveHeights(float t)
    {
        float cutoff = maxFrequency * lowPassFilter;

        for (int m = 0; m < size; m++)
        {
            for (int n = 0; n < size; n++)
            {
                int n_shifted = (n < size / 2) ? n : n - size;
                int m_shifted = (m < size / 2) ? m : m - size;

                Vector2 k = new Vector2(
                    2.0f * Mathf.PI * n_shifted / length,
                    2.0f * Mathf.PI * m_shifted / length
                );
                float k_mag = k.magnitude;

                // Filtro de Baixa Passagem 
                // Se a magnitude da frequência (k_mag) for maior que o limite, zera sua contribuição.
                if (k_mag > cutoff)
                {
                    h_tilde[n, m] = new Complex(0, 0);
                    dx_tilde[n, m] = new Complex(0, 0);
                    dz_tilde[n, m] = new Complex(0, 0);
                    slope_x_tilde[n, m] = new Complex(0, 0);
                    slope_z_tilde[n, m] = new Complex(0, 0);
                    continue;
                }

                float omega = Mathf.Sqrt(g * k_mag);

                float cos_wt = Mathf.Cos(omega * t);
                float sin_wt = Mathf.Sin(omega * t);

                Complex h0_k = h0[n, m];
                Complex h0_minus_k = h0[(size - n) % size, (size - m) % size].Conjugate();

                Complex exp_iwt = new Complex(cos_wt, sin_wt);
                Complex exp_minus_iwt = new Complex(cos_wt, -sin_wt);

                h_tilde[n, m] = h0_k * exp_iwt + h0_minus_k * exp_minus_iwt;

                if (k_mag > 0.0001f)
                {
                    dx_tilde[n, m] = new Complex(0, -k.x / k_mag) * h_tilde[n, m];
                    dz_tilde[n, m] = new Complex(0, -k.y / k_mag) * h_tilde[n, m];
                    slope_x_tilde[n, m] = new Complex(0, k.x) * h_tilde[n, m];
                    slope_z_tilde[n, m] = new Complex(0, k.y) * h_tilde[n, m];
                }
                else
                {
                    dx_tilde[n, m] = new Complex(0, 0);
                    dz_tilde[n, m] = new Complex(0, 0);
                    slope_x_tilde[n, m] = new Complex(0, 0);
                    slope_z_tilde[n, m] = new Complex(0, 0);
                }
            }
        }

        FFT.Perform2D(h_tilde, size, size, -1);
        FFT.Perform2D(dx_tilde, size, size, -1);
        FFT.Perform2D(dz_tilde, size, size, -1);
        FFT.Perform2D(slope_x_tilde, size, size, -1);
        FFT.Perform2D(slope_z_tilde, size, size, -1);
    }

    // Aplica as alturas e deslocamentos calculados à malha
    private void UpdateMesh()
    {
        Vector3[] normals = new Vector3[size * size];

        // Para ponderar o efeito, primeiro encontrar a altura máxima atual na malha
        float maxHeight = 0.001f; // Evita divisão por zero
        for (int i = 0; i < size * size; i++)
        {
            if (h_tilde[i % size, i / size].real > maxHeight)
            {
                maxHeight = h_tilde[i % size, i / size].real;
            }
        }


        for (int i = 0; i < size * size; i++)
        {
            int x = i % size;
            int z = i / size;

            Vector3 baseVertex = baseVertices[i];

            float height = h_tilde[x, z].real;

            displacedVertices[i] = new Vector3(baseVertex.x, height, baseVertex.z);

            float slopeX = slope_x_tilde[x, z].real;
            float slopeZ = slope_z_tilde[x, z].real;
            normals[i] = new Vector3(-slopeX, 1, -slopeZ).normalized;
        }

        mesh.vertices = displacedVertices;
        mesh.normals = normals;
        mesh.RecalculateBounds();
    }

    void OnDrawGizmosSelected()
    {
        foreach (WaveSystem system in waveSystems)
        {
            // Desenha direção do vento para cada sistema de ondas
            Gizmos.DrawLine(Vector3.up, new Vector3(system.wind.x, 1, system.wind.y));
        }
    }
}