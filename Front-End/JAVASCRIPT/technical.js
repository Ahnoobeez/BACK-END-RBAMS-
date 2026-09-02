function switchTab(event, sectionId) {

    event.preventDefault();

    const links = document.querySelectorAll(".tab-nav a");

    links.forEach(link => {
        link.classList.remove("active");
    });

    event.currentTarget.classList.add("active");

    const contents = document.querySelectorAll(".tab-content");

    contents.forEach(content => {
        content.classList.remove("active-content");
        content.classList.add("hidden");
    });

    const target = document.getElementById(sectionId);

    if (target) {
        target.classList.remove("hidden");
        target.classList.add("active-content");
    }
}