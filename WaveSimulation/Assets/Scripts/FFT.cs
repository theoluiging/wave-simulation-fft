using UnityEngine;

// Estrutura para representar um número complexo
public struct Complex
{
    public float real;
    public float imag;

    public Complex(float r, float i)
    {
        real = r;
        imag = i;
    }

    public static Complex operator +(Complex a, Complex b)
    {
        return new Complex(a.real + b.real, a.imag + b.imag);
    }

    public static Complex operator -(Complex a, Complex b)
    {
        return new Complex(a.real - b.real, a.imag - b.imag);
    }

    public static Complex operator *(Complex a, Complex b)
    {
        return new Complex(a.real * b.real - a.imag * b.imag, a.real * b.imag + a.imag * b.real);
    }

    public static Complex operator *(Complex a, float b)
    {
        return new Complex(a.real * b, a.imag * b);
    }
    
    public Complex Conjugate()
    {
        return new Complex(real, -imag);
    }
}

public static class FFT
{
    // Implementação do algoritmo FFT (radix-2)
    // direction = 1 para FFT, direction = -1 para FFT Inversa
    public static void Perform(Complex[] data, int direction)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
            {
                Complex temp = data[i];
                data[i] = data[j];
                data[j] = temp;
            }
        }

        // Algoritmo Cooley-Tukey
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = 2 * Mathf.PI / len * direction;
            Complex wlen = new Complex(Mathf.Cos((float)ang), Mathf.Sin((float)ang));
            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1, 0);
                for (int j = 0; j < len / 2; j++)
                {
                    Complex u = data[i + j];
                    Complex v = data[i + j + len / 2] * w;
                    data[i + j] = u + v;
                    data[i + j + len / 2] = u - v;
                    w = w * wlen;
                }
            }
        }

        // Normalização para a FFT Inversa
        if (direction == -1)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = data[i] * (1.0f / n);
            }
        }
    }

    // Função para FFT 2D
    public static void Perform2D(Complex[,] data, int width, int height, int direction)
    {
        Complex[] row = new Complex[width];
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
                row[i] = data[i, j];
            
            Perform(row, direction);

            for (int i = 0; i < width; i++)
                data[i, j] = row[i];
        }

        Complex[] col = new Complex[height];
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
                col[j] = data[i, j];

            Perform(col, direction);

            for (int j = 0; j < height; j++)
                data[i, j] = col[j];
        }
    }
}