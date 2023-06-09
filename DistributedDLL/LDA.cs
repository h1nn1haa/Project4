/// University of Washington EEP 598/CSEP 590: Neural Devices, Systems, and Computation
/// Project 4
/// Han Diep

using MathNet.Numerics.IntegralTransforms;

namespace DistributedDLL
{
    public partial class DistributedAPI
    {
        // true if classification is seizure positive, false if classification is seizure negative
        private bool _classificationState = false;

        private int WINDOW_SIZE = 178;

        private double intercept = -4.329217463232012;

        private double[] weights =
        {
            2.19163808521538e-05, 5.2234161579607355e-05, 9.584868627956187e-05,
            5.824844894286114e-05, 4.629542877666266e-05, 5.646522578345715e-05,
            0.00012539799100228123, 0.00010390218329056196, 5.660221871514453e-05,
            1.824878454629184e-05, 4.6392276945961145e-05, 3.482134273265125e-06,
            1.5493101133878713e-05, 5.822167401276938e-05, 3.960045372692045e-05,
            -3.9205810614620565e-06, -4.579774771399011e-05, -8.902809969773485e-05,
            -7.854458954804528e-05, 1.0915845417317823e-05, 1.3610078704171552e-05,
            1.644457229284514e-05, -1.67938301724548e-05, -1.6342392772526844e-07,
            -3.04721788859829e-05, 9.53735700852202e-05, 0.000245672527088678,
            0.00020551904558613375, 6.430583141653028e-05, 0.00011319613477075329,
            -2.0843481806203018e-05, 0.00010788123188951819, 2.8581504910916025e-05,
            0.0004997922596641427, 6.31595140233217e-05, 0.0001236763297748901,
            0.0005262716244413752, -4.941324087784573e-05, 0.0003903776927307232,
            0.0005649670299089966, -0.0002003754473961841, -0.00017142920751477182,
            -0.00022134204507561527, 0.00013502421845593777, -0.0006722364036177792,
            -0.0008435392980566357
        };

        /// <summary>
        /// Uses LDA + FFT to classify signal as either seizure positive or seizure negative
        /// </summary>
        /// <param name="signal"></param>
        /// <returns>returns true if seizure positive, false if seizure negative</returns>
        private bool classify(double[] signal)
        {

            // signal is 178, but we must add two extra zeroes to end of array
            Fourier.ForwardReal(signal, 178, FourierOptions.NoScaling);

            double sum = 0;

            for (int i = 0; i <= 45 * 2; i += 2)
            {
                double psd = Math.Sqrt(signal[i] * signal[i] + signal[i + 1] * signal[i + 1]);
                sum += weights[i / 2] * psd;
            }

            if ((sum + intercept) > 0)
            {
                this._classificationState = true;
                return this._classificationState; // seizure positive
            }
            else
            {
                this._classificationState= false;
                return this._classificationState; // seizure negative
            }
        }
    }
}
