function setupPasswordToggle(inputId, toggleId, eyeOpenId, eyeClosedId) {
    const input = document.getElementById(inputId);
    const toggle = document.getElementById(toggleId);
    const eyeOpen = document.getElementById(eyeOpenId);
    const eyeClosed = document.getElementById(eyeClosedId);
    if (!input || !toggle || !eyeOpen || !eyeClosed) return; // guard

    toggle.addEventListener('click', () => {
        const isPwd = input.type === 'password';
        input.type = isPwd ? 'text' : 'password';
        eyeOpen.classList.toggle('hidden', !isPwd);
        eyeClosed.classList.toggle('hidden', isPwd);
    });
}

document.addEventListener('DOMContentLoaded', () => {
    setupPasswordToggle('answer', 'togglePassword', 'eyeOpen', 'eyeClosed');           // <- was "password"
    setupPasswordToggle('password-again', 'togglePasswordAgain', 'eyeOpenAgain', 'eyeClosedAgain');
});