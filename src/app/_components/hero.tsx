import Image from "next/image";
import { ArrowRight, ShieldCheck, MonitorSmartphone } from "lucide-react";

import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { product } from "@/lib/product";
import { StarRating } from "./star-rating";

export function Hero() {
  return (
    <section
      id="top"
      className="relative overflow-hidden pt-14 pb-20 sm:pt-20 sm:pb-28"
    >
      <div
        aria-hidden
        className="pointer-events-none absolute -top-32 right-[-10%] h-[28rem] w-[28rem] rounded-full bg-secondary/25 blur-3xl"
      />
      <div
        aria-hidden
        className="pointer-events-none absolute bottom-[-15%] left-[-10%] h-72 w-72 rounded-full bg-accent/10 blur-3xl"
      />

      <div className="relative mx-auto grid max-w-6xl grid-cols-1 gap-12 px-4 sm:px-6 lg:grid-cols-2 lg:items-center lg:gap-8 lg:px-8">
        <div>
          <div className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-4 py-1.5 text-sm font-medium text-muted-foreground shadow-sm">
            <StarRating rating={product.rating} size={14} />
            <span>
              {product.rating} · {product.ratingCount} ratings ·{" "}
              {product.soldCount} sold
            </span>
          </div>

          <h1 className="mt-6 font-heading text-4xl font-extrabold leading-[1.05] tracking-tight text-foreground sm:text-5xl lg:text-6xl">
            Your Coffee, Perfectly Stirred{" "}
            <span className="text-primary">in 3 Seconds Flat</span>
          </h1>

          <p className="mt-6 max-w-xl text-lg leading-relaxed text-muted-foreground">
            The {product.fullName} blends your coffee, cocoa, or protein
            shake at the tap of a button — no spoon, no clumps, no mess. Just
            fill, tap, and swirl.
          </p>

          <div className="mt-8 flex flex-wrap items-baseline gap-3">
            <span className="font-heading text-4xl font-extrabold text-foreground">
              ${product.priceUSD}
            </span>
            <span className="text-sm text-muted-foreground">
              {product.priceILSNote} · price confirmed at checkout
            </span>
          </div>

          <div className="mt-8 flex flex-wrap items-center gap-4">
            <a
              href={product.affiliateUrl}
              target="_blank"
              rel="noopener noreferrer sponsored"
              className={cn(buttonVariants({ size: "lg" }))}
            >
              Shop Now on AliExpress
              <ArrowRight size={18} aria-hidden />
            </a>
            <a
              href="#how-it-works"
              className={cn(buttonVariants({ variant: "outline", size: "lg" }))}
            >
              See How It Works
            </a>
          </div>
        </div>

        <div className="relative mx-auto w-full max-w-md">
          <div className="relative overflow-hidden rounded-[2rem] border border-border bg-card shadow-2xl shadow-primary/10">
            <Image
              src="/product/product-white.jpg"
              alt="JSB automatic magnetic self-stirring coffee mug with its lid and touch-screen handle"
              width={960}
              height={960}
              priority
              className="h-full w-full object-cover"
            />
          </div>

          <div className="absolute -left-2 top-4 flex items-center gap-2 rounded-2xl border border-border bg-card px-3 py-2 text-xs font-semibold text-foreground shadow-lg sm:-left-8 sm:top-6 sm:px-4 sm:py-2.5 sm:text-sm">
            <ShieldCheck size={16} className="text-primary" aria-hidden />
            304 Stainless Steel
          </div>
          <div className="absolute -right-2 bottom-4 flex items-center gap-2 rounded-2xl border border-border bg-card px-3 py-2 text-xs font-semibold text-foreground shadow-lg sm:-right-8 sm:bottom-6 sm:px-4 sm:py-2.5 sm:text-sm">
            <MonitorSmartphone size={16} className="text-primary" aria-hidden />
            Touch-Screen Display
          </div>
        </div>
      </div>
    </section>
  );
}
