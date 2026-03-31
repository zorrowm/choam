import { createRouter, createWebHistory } from 'vue-router'
import MainLayout from '../layouts/MainLayout.vue'

const router = createRouter({
  history: createWebHistory('/'),
  routes: [
    {
      path: '/',
      component: MainLayout,
      children: [
        {
          path: '',
          name: 'ledger',
          component: () => import('../pages/TransactionsPage.vue'),
        },
        {
          path: 'chat',
          name: 'chat',
          component: () => import('../pages/ChatPage.vue'),
        },
        {
          path: 'analytics',
          name: 'analytics',
          component: () => import('../pages/AnalyticsPage.vue'),
        },
        {
          path: 'settings',
          name: 'settings',
          component: () => import('../pages/SettingsPage.vue'),
        },
      ],
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/',
    },
  ],
})

export default router
