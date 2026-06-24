import { describe, expect, it } from "vitest";
import { buildLabel } from "./version";

describe("buildLabel", () => {
  it("joins version and short SHA with a middle dot", () => {
    expect(buildLabel("1.0.0", "a1b2c3d")).toBe("v1.0.0 · a1b2c3d");
  });

  it("falls back to 'local' when no SHA is available", () => {
    expect(buildLabel("1.0.0", null)).toBe("v1.0.0 · local");
  });

  it("treats an empty SHA as no SHA", () => {
    expect(buildLabel("1.0.0", "")).toBe("v1.0.0 · local");
  });
});
