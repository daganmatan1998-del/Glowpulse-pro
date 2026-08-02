import { product } from "@/lib/product";

export function SiteFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="border-t border-border bg-background py-10">
      <div className="mx-auto max-w-6xl px-4 text-center text-sm text-muted-foreground sm:px-6 lg:px-8">
        <p>
          © {year} {product.name}. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
