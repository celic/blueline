module API
	class Teams < Grape::API

		represent Team, with: API::Entities::Team

		resource :teams do

			desc 'Search for teams'
			params do
				optional :query, type: String
				optional :division, types: [ Integer, Array[Integer] ]
			end
			get do

				# get teams
				teams = Team.all.order:full_name

				# filter by query if provided
				teams = teams.where 'lower(full_name) like :query or lower(abbreviation) like :query', query: "%#{params[:query].downcase}%" if params[:query]

				# filter by division if provided
				teams = teams.where division_id: params[:division] if params[:division]

				# show results
				present :teams, teams
			end

			route_param :abbv do

				desc 'Get a team by abbreviation'
				get do

					# find team
					team = Team.by_abbrev params[:abbv]
					not_found! '404.1', 'Team was not found' unless team

					# show team
					present :team, team, roster: true, recent: true
				end

			end
		end
	end
end
