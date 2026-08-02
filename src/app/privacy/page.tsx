import type { Metadata } from "next";

import { product } from "@/lib/product";
import { PolicyLayout, PolicySection } from "../_components/policy-layout";

export const metadata: Metadata = {
  title: `Privacy Policy — ${product.name}`,
};

export default function PrivacyPolicyPage() {
  return (
    <PolicyLayout title="Privacy Policy" updated="August 2, 2026">
      <PolicySection title="What We Collect">
        <p>
          When you check out, PayPal collects your name, email address,
          shipping address, and payment details to process your order — we
          never see or store your card or bank information ourselves.
        </p>
      </PolicySection>

      <PolicySection title="How We Use It">
        <ul className="list-disc space-y-2 pl-5">
          <li>To process and ship your order</li>
          <li>To send order and shipping updates</li>
          <li>To respond if you contact us for support</li>
        </ul>
      </PolicySection>

      <PolicySection title="Who We Share It With">
        <p>
          We share order details only with the parties needed to fulfill it —
          our payment processor (PayPal) and our shipping/fulfillment
          partners. We don&apos;t sell or rent your information to
          advertisers.
        </p>
      </PolicySection>

      <PolicySection title="Cookies & Tracking">
        <p>
          This site doesn&apos;t use tracking or advertising cookies. Your
          browser may still store basic technical data (like page
          preferences) needed for the site to function.
        </p>
      </PolicySection>

      <PolicySection title="Your Rights">
        <p>
          You can ask us what information we have about you, or ask us to
          delete it, at any time by emailing{" "}
          <a
            href={`mailto:${product.supportEmail}`}
            className="font-medium text-primary underline underline-offset-2"
          >
            {product.supportEmail}
          </a>
          .
        </p>
      </PolicySection>
    </PolicyLayout>
  );
}
