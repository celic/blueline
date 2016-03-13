module API
  class PlayerStats < Grape::API

    represent PlayerStat, with: API::Entities::PlayerStat
    represent GoalieStat, with: API::Entities::GoalieStat

    resource :players do
      route_param :id do
        resource :stats do

          desc 'Get players stats'
          params do
            optional :game_id, type: Integer

            given :game_id do
              optional :end_game_id, type: Integer
            end
          end
          get do

            # find player
            player = Player.find_by id: params[:id]
            not_found! '404.1', 'Player was not found' unless player

            # get player stats
            stats = player.stats

            if params[:game_id]

              # get starting game
              start_game = Game.find_by id: params[:game_id]
              not_found! '404.2', 'Game was not found' unless start_game

              # get game date
              date = start_game.date

              if params[:end_game_id]

                # get ending game
                end_game = Game.find_by id: params[:end_game_id]
                not_found! '404.3', 'Game range was not found' unless end_game

                # end game must be after start game
                unprocessable! '422.1', 'Ending game must be after starting game' if end_game.date < date

                # date is now a range of dates
                date = date..end_game.date
              end

              # join games to be able to query by date
              stats = stats.joins :game

              # get stats on or between dates
              stats = stats.where 'games.date' => date
            end

            # show data
            present :stats, stats
          end

        end
      end
    end
  end
end
