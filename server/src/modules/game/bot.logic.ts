export function makeBotDecision(game: any, botUserId: string) {
  // Определяем текущую фазу игры
  if (game.phase === "WAITING_ROLL") {
    // Бот бросает кубик
    const diceValue = Math.floor(Math.random() * 6) + 1;
    return {
      action: "roll",
      diceValue,
    };
  }

  if (game.phase === "WAITING_MOVE" && game.lastDice) {
    // Бот ходит на выпавшее количество шагов
    return {
      action: "move",
      steps: game.lastDice,
    };
  }

  if (game.phase === "WAITING_ACTION") {
    // Бот случайно выбирает: карту или проект
    const random = Math.random();
    if (random < 0.5) {
      // Выбрать случайную карту
      return {
        action: "card",
        cardId: "bot_card_1",
        effects: { successPoints: Math.floor(Math.random() * 5) + 1 },
      };
    } else {
      // Завершить проект
      return {
        action: "project",
        successPoints: Math.floor(Math.random() * 5) + 1,
      };
    }
  }

  return null;
}