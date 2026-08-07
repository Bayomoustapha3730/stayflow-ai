export const billingPlanCards = [
  {
    name: "Starter",
    monthlyPrice: "$29",
    copy: "Great for new operators starting with a single property.",
    highlights: ["2 properties", "2,500 AI requests", "Email support"],
    trialDays: 14,
    rank: 1
  },
  {
    name: "Growth",
    monthlyPrice: "$99",
    copy: "Balanced option for growing multi-property operations.",
    highlights: ["20 properties", "20,000 AI requests", "Priority support"],
    trialDays: 14,
    rank: 2
  },
  {
    name: "Scale",
    monthlyPrice: "$249",
    copy: "High-volume plan with premium reliability and controls.",
    highlights: ["Unlimited properties", "200,000 AI requests", "Dedicated success manager"],
    trialDays: 7,
    rank: 3
  }
] as const;

export function getPlanRank(planName: string | null | undefined): number | null {
  if (!planName) {
    return null;
  }

  const match = billingPlanCards.find((plan) => plan.name.toLowerCase() === planName.toLowerCase());
  return match?.rank ?? null;
}
