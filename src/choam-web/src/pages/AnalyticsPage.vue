<template>
  <q-page padding>
    <div class="q-mx-auto placeholder-page">
      <h1 class="placeholder-page__title font-headline">Analytics</h1>
      <p class="placeholder-page__subtitle font-label">Coming soon</p>
    </div>
  </q-page>
</template>

<style scoped lang="scss">
.placeholder-page {
  max-width: 1600px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 60vh;

  &__title {
    font-size: 2.5rem;
    font-weight: 700;
    color: white;
    text-transform: uppercase;
    margin: 0;
  }

  &__subtitle {
    color: var(--on-surface-variant);
    margin-top: 8px;
  }
}
</style>
