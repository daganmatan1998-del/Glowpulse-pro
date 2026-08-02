import { Star, Users, PackageCheck, Palette } from "lucide-react";

import { product } from "@/lib/product";

const stats = [
  { icon: Star, value: `${product.rating}★`, label: "Average Rating" },
  { icon: Users, value: `${product.ratingCount}`, label: "Verified Ratings" },
  { icon: PackageCheck, value: `${product.soldCount}+`, label: "Units Sold" },
  { icon: Palette, value: `${product.colorCount}`, label: "Colors Available" },
];

export function TrustBar() {
  return (
    <section className="border-y border-border bg-primary text-primary-foreground">
      <div className="mx-auto grid max-w-6xl grid-cols-2 gap-6 px-4 py-10 sm:grid-cols-4 sm:px-6 lg:px-8">
        {stats.map(({ icon: Icon, value, label }) => (
          <div key={label} className="flex flex-col items-center gap-2 text-center">
            <Icon size={22} className="text-secondary" aria-hidden />
            <span className="font-heading text-2xl font-bold sm:text-3xl">
              {value}
            </span>
            <span className="text-xs uppercase tracking-wide text-primary-foreground/70 sm:text-sm">
              {label}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}
