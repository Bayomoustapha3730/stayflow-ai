import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PrivacyPolicyPage } from "../src/pages/PrivacyPolicyPage";
import { TermsOfServicePage } from "../src/pages/TermsOfServicePage";
import { DataDeletionPage } from "../src/pages/DataDeletionPage";

describe("public legal pages", () => {
  it("renders the privacy policy without authentication and sets the document title", () => {
    render(<PrivacyPolicyPage />);

    expect(screen.getByRole("heading", { level: 1, name: "Privacy Policy" })).toBeInTheDocument();
    expect(document.title).toBe("StayFlow Privacy Policy");
  });

  it("renders the terms of service without authentication and sets the document title", () => {
    render(<TermsOfServicePage />);

    expect(screen.getByRole("heading", { level: 1, name: "Terms of Service" })).toBeInTheDocument();
    expect(document.title).toBe("StayFlow Terms of Service");
  });

  it("renders data deletion instructions without authentication, sets the document title, and links to privacy", () => {
    render(<DataDeletionPage />);

    expect(screen.getByRole("heading", { level: 1, name: "Data Deletion Instructions" })).toBeInTheDocument();
    expect(document.title).toBe("StayFlow Data Deletion Instructions");
    expect(screen.getByRole("link", { name: "StayFlow Privacy Policy" })).toHaveAttribute("href", "/privacy");
  });
});
