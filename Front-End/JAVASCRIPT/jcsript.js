// =========================================
// SIDEBAR
// =========================================

const sidebar = document.querySelector(".sidebar");
const sidebarToggleBtn = document.getElementById("sidebar-toggle");
const sidebarLockBtn = document.getElementById("lock-icon");


let isLocked = true;

// Open / Close Sidebar
function toggleSidebar() {

    sidebar.classList.toggle("close");

}

// Lock / Unlock
function toggleLock() {

    isLocked = !isLocked;

    if (isLocked) {

        sidebarLockBtn.classList.remove("bx-lock-open-alt");
        sidebarLockBtn.classList.add("bx-lock");

    } else {

        sidebarLockBtn.classList.remove("bx-lock");
        sidebarLockBtn.classList.add("bx-lock-open-alt");

    }

}

// Hover kapag unlocked lang
sidebar.addEventListener("mouseenter", () => {

    if (!isLocked) {
        sidebar.classList.remove("close");
    }

});

sidebar.addEventListener("mouseleave", () => {

    if (!isLocked) {
        sidebar.classList.add("close");
    }

});

// Buttons
sidebarToggleBtn.addEventListener("click", toggleSidebar);
sidebarLockBtn.addEventListener("click", toggleLock);

// =========================================
// PAGE LOADER
// =========================================

const menuLinks = document.querySelectorAll(".link");
const mainContent = document.getElementById("main-content");

function loadPage(page) {

    fetch(`../main-content/${page}.html`)
        .then(response => {

            if (!response.ok) {
                throw new Error("Page not found");
            }

            return response.text();

        })

        .then(html => {

            mainContent.innerHTML = html;

            // initialize page scripts
            switch(page){

                case "marketing":
                    if(typeof initializeMarketingPage === "function"){
                        initializeMarketingPage();
                    }
                    break;

                case "technical":
                    if(typeof initializeTechnicalPage === "function"){
                        initializeTechnicalPage();
                    }
                    break;

            }

        })

        .catch(err => {

            console.error(err);

            mainContent.innerHTML = `
                <div style="padding:40px">
                    <h2>404 - Page Not Found</h2>
                    <p>${page}.html does not exist.</p>
                </div>
            `;

        });

}

// menu click
menuLinks.forEach(link => {

    link.addEventListener("click", e => {

        e.preventDefault();

        menuLinks.forEach(item => item.classList.remove("active-link"));

        link.classList.add("active-link");

        loadPage(link.dataset.page);

    });

});

// default page
loadPage("analytics");

// =========================================
// PROFILE MENU
// =========================================

const subMenu = document.getElementById("subMenu");

function toggleMenu() {
    subMenu.classList.toggle("open-menu");
}

// close kapag click sa labas
document.addEventListener("click", function(e){

    if(
        !e.target.closest(".header-right") &&
        !e.target.closest(".sub-menu-wrap")
    ){
        subMenu.classList.remove("open-menu");
    }

});

if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker
            .register('./SW_CachedSite.js')
            .then(reg => console.log('Service Worker: Registered'))
            .catch(err => console.log(`Service Worker: Error: ${err}`));
            
    });
}