export const billingPlanCards = [
  {
    name: "Free",
    monthlyPrice: "$0",
    copy: "Start with essential tools for a small operation.",
    highlights: ["1 property", "1,000 AI requests", "Core support"],
    trialDays: 0,
    rank: 0
  },
  {
    name: "Starter",
    monthlyPrice: "$29",
    copy: "Great for new operators starting with a single property.",
    highlights: ["5 properties", "10,000 AI requests", "Email support"],
    trialDays: 14,
    rank: 1
  },
  {
    name: "Professional",
    monthlyPrice: "$99",
    copy: "Balanced option for growing multi-property operations.",
    highlights: ["30 properties", "50,000 AI requests", "Priority support"],
    trialDays: 14,
    rank: 2
  },
  {
    name: "Enterprise",
    monthlyPrice: "$249",
    copy: "High-volume plan with premium reliability and controls.",
    highlights: ["Unlimited properties", "Unlimited AI requests", "Dedicated success manager"],
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
