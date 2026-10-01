// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim; // Added to access SnapAimEvaluator
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed
{
    public static class SpeedEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            const double min_speed_bonus = 200; // 200 BPM 1/4th
            const double speed_balancing_factor = 40;

            // alternation bpms speed bonus balancing
            const double alt_aim_peak_bonus = 0.75; // controls how high the bonus is at its peak (0.75 = 1.75x maximum multiplier).
            const double alt_aim_target_bpm = 300.0; // the exact bpm where the peak bonus is applied.
            const double alt_aim_bpm_width = 10.0; // controls the width of the affected bpm range (acts as standard deviation).
            const double alt_aim_taper_shape = 3.0; // controls how fast it tapers off. 2.0 is a standard bell curve. higher values create a flatter peak with steeper drop-offs.

            var osuCurrObj = (OsuDifficultyHitObject)current;

            double strainTime = osuCurrObj.AdjustedDeltaTime;
            double doubleTapFeasibility = 1.0 - osuCurrObj.CalculateDoubleTapFeasibility((OsuDifficultyHitObject?)osuCurrObj.Next());

            // Cap deltatime to the OD 300 hitwindow.
            // 0.93 is derived from making sure 260bpm OD8 streams aren't nerfed harshly, whilst 0.92 limits the effect of the cap.
            strainTime /= Math.Clamp((strainTime / osuCurrObj.HitWindowGreat) / 0.93, 0.92, 1);

            // speedBonus will be 0.0 for BPM < 200
            double speedBonus = 0.0;

            // Add additional scaling bonus for streams/bursts higher than 200bpm
            if (DiffUtils.MillisecondsToBPM(strainTime) > min_speed_bonus)
                speedBonus = 0.75 * DiffUtils.Pow((DiffUtils.BPMToMilliseconds(min_speed_bonus) - strainTime) / speed_balancing_factor, 2);

            // Base difficulty with all bonuses
            double speedDifficulty = (1 + speedBonus) * 1000 / strainTime;

            speedDifficulty *= highBpmBonus(osuCurrObj.AdjustedDeltaTime);

            // bonus for speed at uncomfortable alternation bpms
            double snapAim = SnapAimEvaluator.EvaluateDifficultyOf(current, true);

            if (snapAim > 0)
            {
                double effectiveBpm = DiffUtils.MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime, 2);

                // the machine told me i need this
                double baseCalculation = Math.Abs(effectiveBpm - alt_aim_target_bpm) / alt_aim_bpm_width;

                double alternatingBonus = alt_aim_peak_bonus * Math.Exp(-0.5 * Math.Pow(baseCalculation, alt_aim_taper_shape));

                speedDifficulty *= (1 + alternatingBonus);
            }

            // Apply penalty if there's doubletappable doubles
            return speedDifficulty * doubleTapFeasibility;
        }

        private static double highBpmBonus(double ms) => 1 / (1 - DiffUtils.Pow(0.3, ms / 1000));
    }
}
