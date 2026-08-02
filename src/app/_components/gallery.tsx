import Image from "next/image";

import { product } from "@/lib/product";

const images = [
  {
    src: "/product/lifestyle-tray.jpg",
    alt: "Holding the self-stirring mug at the breakfast table with fresh bread and toppings",
  },
  {
    src: "/product/product-white.jpg",
    alt: "The mug on a plain background, showing its handle and touch display",
  },
  {
    src: "/product/hero-swirl.jpg",
    alt: "Top-down view of coffee swirling inside the mug",
  },
  {
    src: "/product/lcd-macro.jpg",
    alt: "Close-up of the LCD touch display showing live temperature",
  },
];

export function Gallery() {
  return (
    <section id="gallery" className="mx-auto max-w-6xl px-4 py-20 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl text-center">
        <h2 className="font-heading text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl">
          See It In Action
        </h2>
        <p className="mt-4 text-lg text-muted-foreground">
          Real photos of the {product.name}.
        </p>
      </div>

      <div className="mt-12 grid grid-cols-2 gap-4 md:grid-cols-4">
        {images.map((img) => (
          <div
            key={img.src}
            className="group relative aspect-square overflow-hidden rounded-2xl border border-border"
          >
            <Image
              src={img.src}
              alt={img.alt}
              fill
              sizes="(min-width: 768px) 25vw, 50vw"
              className="object-cover transition-transform duration-300 group-hover:scale-105"
            />
          </div>
        ))}
      </div>
    </section>
  );
}
