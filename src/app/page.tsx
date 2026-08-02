import { SiteHeader } from "./_components/site-header";
import { Hero } from "./_components/hero";
import { TrustBar } from "./_components/trust-bar";
import { FeatureGrid } from "./_components/feature-grid";
import { HowItWorks } from "./_components/how-it-works";
import { Gallery } from "./_components/gallery";
import { RatingSummary } from "./_components/rating-summary";
import { FAQ } from "./_components/faq";
import { FinalCTA } from "./_components/final-cta";
import { SiteFooter } from "./_components/site-footer";

export default function Home() {
  return (
    <>
      <SiteHeader />
      <main>
        <Hero />
        <TrustBar />
        <FeatureGrid />
        <HowItWorks />
        <Gallery />
        <RatingSummary />
        <FAQ />
        <FinalCTA />
      </main>
      <SiteFooter />
    </>
  );
}
