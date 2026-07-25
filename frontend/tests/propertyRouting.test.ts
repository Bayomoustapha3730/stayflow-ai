import { describe, expect, it } from "vitest";
import { normalizePropertyId, resolvePropertyKnowledgePropertyId } from "../src/utils/propertyRouting";

describe("property routing helpers", () => {
  it("normalizes valid GUID property IDs", () => {
    expect(normalizePropertyId(" 22222222-2222-4222-8222-222222222222 ")).toBe(
      "22222222-2222-4222-8222-222222222222"
    );
  });

  it("rejects invalid property IDs", () => {
    expect(normalizePropertyId("p-1")).toBeNull();
    expect(normalizePropertyId(" ")).toBeNull();
    expect(normalizePropertyId(null)).toBeNull();
  });

  it("prefers selected conversation property IDs over configured fallback", () => {
    expect(
      resolvePropertyKnowledgePropertyId(
        "22222222-2222-4222-8222-222222222222",
        "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
      )
    ).toBe("22222222-2222-4222-8222-222222222222");
  });

  it("uses fallback only when enabled", () => {
    expect(
      resolvePropertyKnowledgePropertyId(
        "",
        "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        true
      )
    ).toBe("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    expect(
      resolvePropertyKnowledgePropertyId(
        "",
        "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        false
      )
    ).toBeNull();
  });
});
