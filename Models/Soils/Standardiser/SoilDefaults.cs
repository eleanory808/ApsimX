﻿namespace Models.Soils.Standardiser
{
    using APSIM.Shared.Utilities;
    using Models.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>Implements the defaults as listed in the soil protocol.</summary>
    /// <remarks>
    /// A PROTOCOL FOR THE DEVELOPMENT OF SOIL PARAMETER VALUES FOR USE IN APSIM
    ///     Neal Dalgliesh, Zvi Hochman, Neil Huth and Dean Holzworth
    /// </remarks>
    public class SoilDefaults
    {
        /// <summary>Fills in missing values where possible.</summary>
        /// <param name="soil">The soil.</param>
        public static void FillInMissingValues(Soil soil)
        {
            AddPredictedCrops(soil);
            CheckAnalysisForMissingValues(soil);

            var water = soil.FindChild<Physical>();
            if (water != null)
            {
                var crops = water.FindAllChildren<SoilCrop>().Cast<SoilCrop>().ToArray();

                foreach (var crop in crops)
                {
                    if (crop.XF == null)
                    {
                        crop.XF = MathUtilities.CreateArrayOfValues(1.0, water.Thickness.Length);
                        crop.XFMetadata = StringUtilities.CreateStringArray("Estimated", water.Thickness.Length);
                    }
                    if (crop.KL == null)
                        FillInKLForCrop(crop);

                    CheckCropForMissingValues(crop, soil);

                    // Modify wheat crop for sub soil constraints.
                    if (crop.Name.Equals("wheat", StringComparison.InvariantCultureIgnoreCase))
                        ModifyKLForSubSoilConstraints(crop, soil);
                }
            }

            var samples = soil.FindAllChildren<Sample>().Cast<Sample>().ToArray();
            foreach (Sample sample in samples)
                CheckSampleForMissingValues(sample, soil);

            // Make sure there are the correct number of KS values.
            if (water?.KS != null && water?.KS.Length > 0)
                water.KS = FillMissingValues(water.KS, water.Thickness.Length, 0.0);
        }

        private static string[] cropNames = {"Wheat", "Oats",
                                             "Sorghum", "Barley", "Chickpea", "Mungbean", "Cotton", "Canola",
                                             "PigeonPea", "Maize", "Cowpea", "Sunflower", "Fababean", "Lucerne",
                                             "Lupin", "Lentil", "Triticale", "Millet", "Soybean" };

        private static double[] defaultKLThickness = new double[] { 150, 300, 600, 900, 1200, 1500, 1800 };
        private static double[,] defaultKLs =  {{0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.07,   0.07,   0.07,   0.05,   0.05,   0.04,   0.03},
                                                {0.07,   0.07,   0.07,   0.05,   0.05,   0.03,   0.02},
                                                {0.06,   0.06,   0.06,   0.06,   0.06,   0.06,   0.06},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.00,   0.00},
                                                {0.10,   0.10,   0.10,   0.10,   0.09,   0.07,   0.05},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.06,   0.06,   0.06,   0.05,   0.04,   0.02,   0.01},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.10,   0.10,   0.08,   0.06,   0.04,   0.02,   0.01},
                                                {0.08,   0.08,   0.08,   0.08,   0.06,   0.04,   0.03},
                                                {0.10,   0.10,   0.10,   0.10,   0.09,   0.09,   0.09},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01},
                                                {0.07,   0.07,   0.07,   0.04,   0.02,   0.01,   0.01},
                                                {0.07,   0.07,   0.07,   0.05,   0.05,   0.04,   0.03},
                                                {0.06,   0.06,   0.06,   0.04,   0.04,   0.02,   0.01}};

        /// <summary>Fills in KL for crop.</summary>
        /// <param name="crop">The crop.</param>
        private static void FillInKLForCrop(SoilCrop crop)
        {
            if (crop.Name == null)
                throw new Exception("Crop has no name");
            int i = StringUtilities.IndexOfCaseInsensitive(cropNames, crop.Name);
            if (i != -1)
            {
                var water = crop.Parent as Physical;

                double[] KLs = GetRowOfArray(defaultKLs, i);

                double[] cumThickness = SoilUtilities.ToCumThickness(water.Thickness);
                crop.KL = new double[water.Thickness.Length];
                for (int l = 0; l < water.Thickness.Length; l++)
                {
                    bool didInterpolate;
                    crop.KL[l] = MathUtilities.LinearInterpReal(cumThickness[l], defaultKLThickness, KLs, out didInterpolate);
                }
            }
        }

        /// <summary>Gets the row of a 2 dimensional array.</summary>
        /// <param name="array">The array.</param>
        /// <param name="row">The row index</param>
        /// <returns>The values in the specified row.</returns>
        private static double[] GetRowOfArray(double[,] array, int row)
        {
            List<double> values = new List<double>();
            for (int col = 0; col < array.GetLength(1); col++)
                values.Add(array[row, col]);

            return values.ToArray();
        }

        /// <summary>Checks the analysis for missing values.</summary>
        /// <param name="soil">The soil.</param>
        private static void CheckAnalysisForMissingValues(Soil soil)
        {
            var analysis = soil.FindChild<Chemical>();

            analysis.CL = FillMissingValues(analysis.CL, analysis.Thickness.Length, 0);
            analysis.EC = FillMissingValues(analysis.EC, analysis.Thickness.Length, 0);
            analysis.ESP = FillMissingValues(analysis.ESP, analysis.Thickness.Length, 0);
            analysis.PH = FillMissingValues(analysis.PH, analysis.Thickness.Length, 7.0);
            analysis.NO3N = FillMissingValues(analysis.NO3N, analysis.Thickness.Length, 0.1);
            analysis.NH4N = FillMissingValues(analysis.NH4N, analysis.Thickness.Length, 0.01);
        }

        /// <summary>Changes all missing values in an array to a valid value.</summary>
        /// <param name="values">The values to in fill.</param>
        /// <param name="numValues">The number of values that should exist.</param>
        /// <param name="defaultValue">The value to use if can't find any other value.</param>
        private static double[] FillMissingValues(double[] values, int numValues, double defaultValue)
        {
            Array.Resize(ref values, numValues);
            for (int i = 0; i < numValues; i++)
            {
                if (double.IsNaN(values[i]))
                {
                    double validValue = 0;
                    if (i == 0)
                        validValue = FindFirstValueInArray(values);
                    else
                        validValue = values[i - 1];

                    if (double.IsNaN(validValue))
                        values[i] = defaultValue;
                    else
                        values[i] = validValue;
                }
            }
            return values;
        }

        /// <summary>
        /// Find the first non NaN value in the array.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private static double FindFirstValueInArray(double[] values)
        {
            for (int j = 0; j < values.Length; j++)
                if (!double.IsNaN(values[j]))
                    return values[j];

            return double.NaN;
        }

        /// <summary>Checks the crop for missing values.</summary>
        /// <param name="crop">The crop.</param>
        /// <param name="soil">The soil.</param>
        private static void CheckCropForMissingValues(SoilCrop crop, Soil soil)
        {
            var water = soil.FindChild<Physical>();

            for (int i = 0; i < water.Thickness.Length; i++)
            {
                if (crop.LL != null && double.IsNaN(crop.LL[i]))
                    crop.LL[i] = water.LL15[i];
                if (crop.KL != null && double.IsNaN(crop.KL[i]))
                    crop.KL[i] = 0.06;
                if (crop.XF != null && double.IsNaN(crop.XF[i]))
                    crop.XF[i] = 1;
            }
        }

        /// <summary>Checks the sample for missing values.</summary>
        /// <param name="sample">The sample.</param>
        /// <param name="soil">The soil.</param>
        private static void CheckSampleForMissingValues(Sample sample, Soil soil)
        {
            if (!MathUtilities.ValuesInArray(sample.SW))
                sample.SW = null;
            if (sample.NO3 != null && !MathUtilities.ValuesInArray(sample.NO3))
                sample.NO3 = null;
            if (sample.NH4 != null && !MathUtilities.ValuesInArray(sample.NH4))
                sample.NH4 = null;
            if (!MathUtilities.ValuesInArray(sample.CL))
                sample.CL = null;
            if (!MathUtilities.ValuesInArray(sample.EC))
                sample.EC = null;
            if (!MathUtilities.ValuesInArray(sample.ESP))
                sample.ESP = null;
            if (!MathUtilities.ValuesInArray(sample.PH))
                sample.PH = null;
            if (!MathUtilities.ValuesInArray(sample.OC))
                sample.OC = null;

            if (sample.SW != null)
                sample.SW = MathUtilities.FixArrayLength(sample.SW, sample.Thickness.Length);
            if (sample.CL != null)
                sample.CL = MathUtilities.FixArrayLength(sample.CL, sample.Thickness.Length);
            if (sample.EC != null)
                sample.EC = MathUtilities.FixArrayLength(sample.EC, sample.Thickness.Length);
            if (sample.ESP != null)
                sample.ESP = MathUtilities.FixArrayLength(sample.ESP, sample.Thickness.Length);
            if (sample.PH != null)
                sample.PH = MathUtilities.FixArrayLength(sample.PH, sample.Thickness.Length);
            if (sample.OC != null)
                sample.OC = MathUtilities.FixArrayLength(sample.OC, sample.Thickness.Length);

            var physical = soil.FindChild<Physical>();
            if (physical != null)
            {
                double[] ll15 = Layers.LL15Mapped(physical, sample.Thickness);
                for (int i = 0; i < sample.Thickness.Length; i++)
                {
                    if (sample.SW != null && double.IsNaN(sample.SW[i]))
                        sample.SW[i] = ll15[i];
                }
            }
        }

        /// <summary>
        /// The black vertosol crop list
        /// </summary>
        private static string[] BlackVertosolCropList = new string[] { "Wheat", "Sorghum", "Cotton" };
        /// <summary>
        /// The grey vertosol crop list
        /// </summary>
        private static string[] GreyVertosolCropList = new string[] { "Wheat", "Sorghum", "Cotton", "Barley", "Chickpea", "Fababean", "Mungbean" };
        /// <summary>
        /// The predicted thickness
        /// </summary>
        private static double[] PredictedThickness = new double[] { 150, 150, 300, 300, 300, 300, 300 };
        /// <summary>
        /// The predicted xf
        /// </summary>
        private static double[] PredictedXF = new double[] { 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00 };
        /// <summary>
        /// The wheat kl
        /// </summary>
        private static double[] WheatKL = new double[] { 0.06, 0.06, 0.06, 0.04, 0.04, 0.02, 0.01 };
        /// <summary>
        /// The sorghum kl
        /// </summary>
        private static double[] SorghumKL = new double[] { 0.07, 0.07, 0.07, 0.05, 0.05, 0.04, 0.03 };
        /// <summary>
        /// The barley kl
        /// </summary>
        private static double[] BarleyKL = new double[] { 0.07, 0.07, 0.07, 0.05, 0.05, 0.03, 0.02 };
        /// <summary>
        /// The chickpea kl
        /// </summary>
        private static double[] ChickpeaKL = new double[] { 0.06, 0.06, 0.06, 0.06, 0.06, 0.06, 0.06 };
        /// <summary>
        /// The mungbean kl
        /// </summary>
        private static double[] MungbeanKL = new double[] { 0.06, 0.06, 0.06, 0.04, 0.04, 0.00, 0.00 };
        /// <summary>
        /// The cotton kl
        /// </summary>
        private static double[] CottonKL = new double[] { 0.10, 0.10, 0.10, 0.10, 0.09, 0.07, 0.05 };
        /// <summary>
        /// The canola kl
        /// </summary>
        /// private static double[] CanolaKL = new double[] { 0.06, 0.06, 0.06, 0.04, 0.04, 0.02, 0.01 };
        /// <summary>
        /// The pigeon pea kl
        /// </summary>
        /// private static double[] PigeonPeaKL = new double[] { 0.06, 0.06, 0.06, 0.05, 0.04, 0.02, 0.01 };
        /// <summary>
        /// The maize kl
        /// </summary>
        /// private static double[] MaizeKL = new double[] { 0.06, 0.06, 0.06, 0.04, 0.04, 0.02, 0.01 };
        /// <summary>
        /// The cowpea kl
        /// </summary>
        /// private static double[] CowpeaKL = new double[] { 0.06, 0.06, 0.06, 0.04, 0.04, 0.02, 0.01 };
        /// <summary>
        /// The sunflower kl
        /// </summary>
        /// private static double[] SunflowerKL = new double[] { 0.01, 0.01, 0.08, 0.06, 0.04, 0.02, 0.01 };
        /// <summary>
        /// The fababean kl
        /// </summary>
        private static double[] FababeanKL = new double[] { 0.08, 0.08, 0.08, 0.08, 0.06, 0.04, 0.03 };
        /// <summary>
        /// The lucerne kl
        /// </summary>
        /// private static double[] LucerneKL = new double[] { 0.01, 0.01, 0.01, 0.01, 0.09, 0.09, 0.09 };
        /// <summary>
        /// The perennial kl
        /// </summary>
        /// private static double[] PerennialKL = new double[] { 0.01, 0.01, 0.01, 0.01, 0.09, 0.07, 0.05 };

        /// <summary>
        /// 
        /// </summary>
        private class BlackVertosol
        {
            /// <summary>
            /// The cotton a
            /// </summary>
            internal static double[] CottonA = new double[] { 0.832, 0.868, 0.951, 0.988, 1.043, 1.095, 1.151 };
            /// <summary>
            /// The sorghum a
            /// </summary>
            internal static double[] SorghumA = new double[] { 0.699, 0.802, 0.853, 0.907, 0.954, 1.003, 1.035 };
            /// <summary>
            /// The wheat a
            /// </summary>
            internal static double[] WheatA = new double[] { 0.124, 0.049, 0.024, 0.029, 0.146, 0.246, 0.406 };

            /// <summary>
            /// The cotton b
            /// </summary>
            internal static double CottonB = -0.0070;
            /// <summary>
            /// The sorghum b
            /// </summary>
            internal static double SorghumB = -0.0038;
            /// <summary>
            /// The wheat b
            /// </summary>
            internal static double WheatB = 0.0116;

        }
        /// <summary>
        /// 
        /// </summary>
        private class GreyVertosol
        {
            /// <summary>
            /// The cotton a
            /// </summary>
            internal static double[] CottonA = new double[] { 0.853, 0.851, 0.883, 0.953, 1.022, 1.125, 1.186 };
            /// <summary>
            /// The sorghum a
            /// </summary>
            internal static double[] SorghumA = new double[] { 0.818, 0.864, 0.882, 0.938, 1.103, 1.096, 1.172 };
            /// <summary>
            /// The wheat a
            /// </summary>
            internal static double[] WheatA = new double[] { 0.660, 0.655, 0.701, 0.745, 0.845, 0.933, 1.084 };
            /// <summary>
            /// The barley a
            /// </summary>
            internal static double[] BarleyA = new double[] { 0.847, 0.866, 0.835, 0.872, 0.981, 1.036, 1.152 };
            /// <summary>
            /// The chickpea a
            /// </summary>
            internal static double[] ChickpeaA = new double[] { 0.435, 0.452, 0.481, 0.595, 0.668, 0.737, 0.875 };
            /// <summary>
            /// The fababean a
            /// </summary>
            internal static double[] FababeanA = new double[] { 0.467, 0.451, 0.396, 0.336, 0.190, 0.134, 0.084 };
            /// <summary>
            /// The mungbean a
            /// </summary>
            internal static double[] MungbeanA = new double[] { 0.779, 0.770, 0.834, 0.990, 1.008, 1.144, 1.150 };
            /// <summary>
            /// The cotton b
            /// </summary>
            internal static double CottonB = -0.0082;
            /// <summary>
            /// The sorghum b
            /// </summary>
            internal static double SorghumB = -0.007;
            /// <summary>
            /// The wheat b
            /// </summary>
            internal static double WheatB = -0.0032;
            /// <summary>
            /// The barley b
            /// </summary>
            internal static double BarleyB = -0.0051;
            /// <summary>
            /// The chickpea b
            /// </summary>
            internal static double ChickpeaB = 0.0029;
            /// <summary>
            /// The fababean b
            /// </summary>
            internal static double FababeanB = 0.02455;
            /// <summary>
            /// The mungbean b
            /// </summary>
            internal static double MungbeanB = -0.0034;
        }

        /// <summary>
        /// Return a list of predicted crop names or an empty string[] if none found.
        /// </summary>
        /// <param name="soil">The soil.</param>
        /// <returns></returns>
        private static void AddPredictedCrops(Soil soil)
        {
            if (soil.SoilType != null)
            {
                string[] predictedCropNames = null;
                if (soil.SoilType.Equals("Black Vertosol", StringComparison.CurrentCultureIgnoreCase))
                    predictedCropNames = BlackVertosolCropList;
                else if (soil.SoilType.Equals("Grey Vertosol", StringComparison.CurrentCultureIgnoreCase))
                    predictedCropNames = GreyVertosolCropList;

                if (predictedCropNames != null)
                {
                    var water = soil.FindChild<Physical>();
                    var crops = water.FindAllChildren<SoilCrop>().ToList();

                    foreach (string cropName in predictedCropNames)
                    {
                        // if a crop parameterisation already exists for this crop then don't add a predicted one.
                        if (crops.Find(c => c.Name.Equals(cropName, StringComparison.InvariantCultureIgnoreCase)) == null)
                            crops.Add(PredictedCrop(soil, cropName));
                    }
                }
            }
        }

        /// <summary>
        /// Return a predicted SoilCrop for the specified crop name or null if not found.
        /// </summary>
        /// <param name="soil">The soil.</param>
        /// <param name="CropName">Name of the crop.</param>
        /// <returns></returns>
        private static SoilCrop PredictedCrop(Soil soil, string CropName)
        {
            double[] A = null;
            double B = double.NaN;
            double[] KL = null;

            if (soil.SoilType == null)
                return null;

            if (soil.SoilType.Equals("Black Vertosol", StringComparison.CurrentCultureIgnoreCase))
            {
                if (CropName.Equals("Cotton", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = BlackVertosol.CottonA;
                    B = BlackVertosol.CottonB;
                    KL = CottonKL;
                }
                else if (CropName.Equals("Sorghum", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = BlackVertosol.SorghumA;
                    B = BlackVertosol.SorghumB;
                    KL = SorghumKL;
                }
                else if (CropName.Equals("Wheat", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = BlackVertosol.WheatA;
                    B = BlackVertosol.WheatB;
                    KL = WheatKL;
                }
            }
            else if (soil.SoilType.Equals("Grey Vertosol", StringComparison.CurrentCultureIgnoreCase))
            {
                if (CropName.Equals("Cotton", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.CottonA;
                    B = GreyVertosol.CottonB;
                    KL = CottonKL;
                }
                else if (CropName.Equals("Sorghum", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.SorghumA;
                    B = GreyVertosol.SorghumB;
                    KL = SorghumKL;
                }
                else if (CropName.Equals("Wheat", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.WheatA;
                    B = GreyVertosol.WheatB;
                    KL = WheatKL;
                }
                else if (CropName.Equals("Barley", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.BarleyA;
                    B = GreyVertosol.BarleyB;
                    KL = BarleyKL;
                }
                else if (CropName.Equals("Chickpea", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.ChickpeaA;
                    B = GreyVertosol.ChickpeaB;
                    KL = ChickpeaKL;
                }
                else if (CropName.Equals("Fababean", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.FababeanA;
                    B = GreyVertosol.FababeanB;
                    KL = FababeanKL;
                }
                else if (CropName.Equals("Mungbean", StringComparison.CurrentCultureIgnoreCase))
                {
                    A = GreyVertosol.MungbeanA;
                    B = GreyVertosol.MungbeanB;
                    KL = MungbeanKL;
                }
            }


            if (A == null)
                return null;

            var physical = soil.FindChild<IPhysical>();
            double[] LL = PredictedLL(physical, A, B);
            LL = Layers.MapConcentration(LL, PredictedThickness, physical.Thickness, LL.Last());
            KL = Layers.MapConcentration(KL, PredictedThickness, physical.Thickness, KL.Last());
            double[] XF = Layers.MapConcentration(PredictedXF, PredictedThickness, physical.Thickness, PredictedXF.Last());
            string[] Metadata = StringUtilities.CreateStringArray("Estimated", physical.Thickness.Length);

            return new SoilCrop()
            {
                Name = CropName,
                LL = LL,
                LLMetadata = Metadata,
                KL = KL,
                KLMetadata = Metadata,
                XF = XF,
                XFMetadata = Metadata
            };
        }

        /// <summary>
        /// Calculate and return a predicted LL from the specified A and B values.
        /// </summary>
        /// <param name="physical">The soil physical properties.</param>
        /// <param name="A">a.</param>
        /// <param name="B">The b.</param>
        /// <returns></returns>
        private static double[] PredictedLL(IPhysical physical, double[] A, double B)
        {
            double[] LL15 = Layers.LL15Mapped(physical, PredictedThickness);
            double[] DUL = Layers.DULMapped(physical, PredictedThickness);
            double[] LL = new double[PredictedThickness.Length];
            for (int i = 0; i != PredictedThickness.Length; i++)
            {
                double DULPercent = DUL[i] * 100.0;
                LL[i] = DULPercent * (A[i] + B * DULPercent);
                LL[i] /= 100.0;

                // Bound the predicted LL values.
                LL[i] = Math.Max(LL[i], LL15[i]);
                LL[i] = Math.Min(LL[i], DUL[i]);
            }

            //  make the top 3 layers the same as the top 3 layers of LL15
            if (LL.Length >= 3)
            {
                LL[0] = LL15[0];
                LL[1] = LL15[1];
                LL[2] = LL15[2];
            }
            return LL;
        }


        /// <summary>Standard thicknesses</summary>
        private static readonly double[] StandardThickness = new double[] { 100, 100, 200, 200, 200, 200, 200 };
        /// <summary>Standard Kls</summary>
        private static readonly double[] StandardKL = new double[] { 0.06, 0.06, 0.04, 0.04, 0.04, 0.04, 0.02 };

        /// <summary>
        /// Modify the KL values for subsoil constraints.
        /// </summary>
        /// <remarks>
        /// From:
        /// Hochman, Z., Dang, Y.P., Schwenke, G.D., Dalgliesh, N.P., Routley, R., McDonald, M., 
        ///     Daniells, I.G., Manning, W., Poulton, P.L., 2007. 
        ///     Simulating the effects of saline and sodic subsoils on wheat crops 
        ///     growing on Vertosols. Australian Journal of Agricultural Research 58, 802–810. doi:10.1071/ar06365
        /// </remarks>
        /// <param name="crop"></param>
        /// <param name="soil">The soil the crop belongs to.</param>
        private static void ModifyKLForSubSoilConstraints(SoilCrop crop, Soil soil)
        {
            var soilPhysical = soil.FindChild<IPhysical>();
            var initialConditions = soil.Children.Find(child => child is Sample) as Sample;
            double[] cl = initialConditions.CL;
            if (MathUtilities.ValuesInArray(cl))
            {
                crop.KL = Layers.MapConcentration(StandardKL, StandardThickness, soilPhysical.Thickness, StandardKL.Last());
                for (int i = 0; i < soilPhysical.Thickness.Length; i++)
                    crop.KL[i] *= Math.Min(1.0, 4.0 * Math.Exp(-0.005 * cl[i]));
            }
            else
            {
                double[] esp = initialConditions.ESP;
                if (MathUtilities.ValuesInArray(esp))
                {
                    crop.KL = Layers.MapConcentration(StandardKL, StandardThickness, soilPhysical.Thickness, StandardKL.Last());
                    for (int i = 0; i < soilPhysical.Thickness.Length; i++)
                        crop.KL[i] *= Math.Min(1.0, 10.0 * Math.Exp(-0.15 * esp[i]));
                }
                else
                {
                    double[] ec = initialConditions.EC;
                    if (MathUtilities.ValuesInArray(ec))
                    {
                        crop.KL = Layers.MapConcentration(StandardKL, StandardThickness, soilPhysical.Thickness, StandardKL.Last());
                        for (int i = 0; i < soilPhysical.Thickness.Length; i++)
                            crop.KL[i] *= Math.Min(1.0, 3.0 * Math.Exp(-1.3 * ec[i]));
                    }
                }
            }
        }
    }
}
