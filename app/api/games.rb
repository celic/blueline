module API
	class Games < Grape::API

		represent Game, with: API::Entities::Game

		resource :games do

			desc 'Search for games'
			params do
				optional :date, type: Date
				optional :team_id, type: Integer

				given :team_id do
					optional :opponent_id, type: Integer
					optional :home, type: Boolean
				end
			end
			get do

				# find games
				games = Game.all.order(date: :desc).limit(15)

				# filter by date if provided
				games = games.where date: params[:date] if params[:date]

				if params[:team_id]

					# find games with team matching criteria
					games =	if params[:home].nil?

								games.participant params[:team_id]

							elsif params[:home]

								games.where home_id: params[:team_id]

							else

								games.where away_id: params[:team_id]
							end

					# filter by opponent also if provided
					games = games.participant params[:opponent_id] if params[:opponent_id]
				end

				# show games
				present :games, games
			end
		end
	end
end
