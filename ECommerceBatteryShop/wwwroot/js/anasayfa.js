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