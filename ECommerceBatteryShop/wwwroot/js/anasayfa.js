document.addEventListener('alpine:init', () => {
    Alpine.data('carousel', ({ visible = 4 } = {}) => ({
        i: 0, visible, maxIndex: 0,
        init() {
            this.$nextTick(() => {
                const track = this.$refs.track;
                const items = track.querySelectorAll('[data-item]');
                const gap = parseFloat(getComputedStyle(track).gap || 16);
                this.itemWidth = items[0]?.offsetWidth + gap || 0;
                this.count = items.length;
                this.maxIndex = Math.max(0, this.count - this.visible);
            });
        },
        move(dir) {
            this.i = Math.max(0, Math.min(this.i + dir, this.maxIndex));
            this.$refs.track.scrollTo({ left: this.i * this.itemWidth, behavior: 'smooth' });
        }
    }));
});

function heroCarousel() {
    return {
        slides: [
            { src: '/img/herobanner.png', alt: 'Batteries', title: 'Her an için güç', text: 'Premium bataryalar ve enerji çözümleri kapınıza getirilir.', ctaText: 'Ürünler', ctaHref: '/Product/Index' },
            { src: '/img/dayı_aspilsan_banner.jpg', alt: 'Aspilsan şarjlı piller', title: 'Yerli ve milli güç', text: 'Daha az atık, daha fazla tasarruf.', ctaText: 'Şimdi keşfet', ctaHref: '/Product/Index?cat=rechargeable' },
            { src: '/img/herobanner_2.png', alt: 'Şarjlı piller', title: 'Şarj edilebilir güç', text: 'Daha az atık, daha fazla tasarruf.', ctaText: 'Şimdi keşfet', ctaHref: '/Product/Index?cat=rechargeable' }
        ],
        current: 0,
        timer: null,
        intervalMs: 6000,
        init(el) {
            this.start();
            // basic swipe
            let startX = 0;
            el.addEventListener('touchstart', e => startX = e.touches[0].clientX, { passive: true });
            el.addEventListener('touchend', e => {
                const dx = e.changedTouches[0].clientX - startX;
                if (Math.abs(dx) > 40) (dx < 0 ? this.next() : this.prev());
            }, { passive: true });
            // pause on hover (desktop)
            el.addEventListener('mouseenter', () => this.stop());
            el.addEventListener('mouseleave', () => this.start());
        },
        start() { this.stop(); this.timer = setInterval(() => this.next(), this.intervalMs); },
        stop() { if (this.timer) clearInterval(this.timer); this.timer = null; },
        next() { this.current = (this.current + 1) % this.slides.length; },
        prev() { this.current = (this.current - 1 + this.slides.length) % this.slides.length; },
        go(i) { this.current = i; this.start(); }
    }
}